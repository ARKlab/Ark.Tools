using Ark.Reference.Common;
using Ark.Reference.Core.Application;
using Ark.Reference.Core.Application.Config;
using Ark.Reference.Core.WebInterface;
using Ark.Tools.Http;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus.Tests;

using AwesomeAssertions;

using Flurl.Http;
using Flurl.Http.Configuration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NLog;

using NodaTime;

using Polly;

using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;

using Reqnroll;

using SimpleInjector;

using System;
using System.IO;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize]

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    public sealed class TestHost : IDisposable
    {
        private const string _baseUri = "https://localhost:5001";

        public static ApiHostConfig? TestConfig { get; private set; }
        public static ICoreDataContextConfig DBConfig => TestConfig ?? throw new InvalidOperationException("TestConfig is null");

        public static IHost Server { get => _server ?? throw new InvalidOperationException("_server is null"); set => _server = value; }
        public static ArkFlurlClientFactory Factory { get => _factory ?? throw new InvalidOperationException("_server is null"); set => _factory = value; }

        private static ArkFlurlClientFactory? _factory;
        public static readonly TestEnv Env = new();

        private static ScenarioContext? _scenarioContext;
        private static IHost? _server;
        private AwesomeAssertions.Execution.AssertionScope? _afterScenarioAssertionScope;

        [BeforeScenario(Order = 0)]
        public void Set(ScenarioContext ctx)
        {
            _scenarioContext = ctx;
        }

        [AfterScenario(Order = int.MinValue)]
        public void EnsureAllHooksRun()
        {
            // SPECFLOW: If a hook throws an unhandled exception, subsequent hooks of the same type are not executed.
            // SPECFLOW: If you want to ensure that all hooks of the same types are executed, you need to handle your exceptions manually.
            // Thus we use FluentAssertion scope
            _afterScenarioAssertionScope = new AwesomeAssertions.Execution.AssertionScope();
        }

        [AfterScenario(Order = int.MaxValue)]
        public void ClearContext()
        {
            try
            {
                _afterScenarioAssertionScope?.Dispose();
            }
            catch
            {
            }
            _afterScenarioAssertionScope = null;
            _scenarioContext = null;
        }

        [Then("I wait background bus to idle and outbox to be empty")]
        [When("I wait background bus to idle and outbox to be empty")]
        public Task ThenIWaitBackgroundBusToIdleAndOutboxToBeEmpty()
        {
            return _backgroundBus(false);
        }


        [When("I wait background bus to idle and outbox to be empty ignoring scheduled message")]
        public Task ThenIWaitBackgroundBusToIdleAndOutboxToBeEmptyIgnoringScheduledMessage()
        {
            return _backgroundBus(true);
        }

        private async Task _backgroundBus(bool ignoreDeferred)
        {
            using var _ = new AwesomeAssertions.Execution.AssertionScope();

            var ctx = Server.Services.GetRequiredService<Container>().GetInstance<IOutboxAsyncContextFactory>();

            var (inqueue, inprocess, deferred, outbox, errorMessages) =
                await Policy
                    .HandleResult<(int inqueue, int inprocess, int deferred, int outbox, int errorMessages)>(
                        (c) =>
                        {
                            int def = c.deferred;
                            return c.errorMessages == 0 && (c.inqueue + c.inprocess + def + c.outbox) == 0;
                        }
                    ) // if true, go again
                    .WaitAndRetryAsync(1, i => TimeSpan.FromMilliseconds(100))
                    .WrapAsync(
                        Policy
                            .HandleResult<(int inqueue, int inprocess, int deferred, int outbox, int errorMessages)>(
                                (c) =>
                                {
                                    int def = c.deferred;
                                    return c.errorMessages == 0 && (c.inqueue + c.inprocess + def + c.outbox) > 0; // if true, go again
                                }
                            )
                            .WaitAndRetryAsync(600, _ => TimeSpan.FromMilliseconds(100))
                     )
                    .ExecuteAsync(async () =>
                    {
                        var inqueue = Env.RebusNetwork.Count();
                        var inprocess = InProcessMessageInspectorStep.Count;
                        var errorMessages = Env.RebusNetwork.Count("error");
                        var due = ignoreDeferred ? 0 : TestsInMemoryTimeoutManager.DueCount;


                        await using var outbox = await ctx.CreateAsync();
                        var outboxCount = await outbox.CountAsync();
                        await outbox.CommitAsync();

                        return (inqueue, inprocess, due, outboxCount, errorMessages);
                    });

            errorMessages.Should().Be(0);
            inqueue.Should().Be(0);
            inprocess.Should().Be(0);
            deferred.Should().Be(0);
            outbox.Should().Be(0);
        }

        [AfterScenario(Order = int.MaxValue - 1)]
        public async Task ClearRebus()
        {
            using var drainer = DrainableInMemTransport.Drain();
            do
            {
                {
                    await using var outbox = await Server.Services.GetRequiredService<Container>().GetInstance<IOutboxAsyncContextFactory>().CreateAsync();
                    await outbox.ClearAsync();
                    await outbox.CommitAsync();
                }
                TestsInMemoryTimeoutManager.ClearPendingDue();
                Env.RebusNetwork.Reset();

                while (InProcessMessageInspectorStep.Count > 0)
                    await Task.Delay(100);

            } while (drainer.StillDraining);

        }

        [BeforeTestRun(Order = 0)]
        public static void BeforeTests0()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "SpecFlow");
            GlobalInit.InitStatics();
        }

        [BeforeTestRun]
        public static void BeforeTests()
        {
            //OutboxMessageConstants.OutboxWaitTimeSpan = TimeSpan.FromMilliseconds(50);

            //ApplicationConstants.ComputeIdleDetectWindow = TimeSpan.FromSeconds(1);

            var builder = Program.GetHostBuilder([])
                .ConfigureWebHost(wh =>
                {
                    wh.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddTransient<Func<ScenarioContext>>(s => () => _scenarioContext ?? throw new InvalidOperationException("ScenarioContext is accessed outside of a Scenario."));
                        services.AddSingleton(Env.RebusNetwork);
                        services.AddSingleton(Env.RebusSubscriber);
                        services.AddSingleton<IClock>(MockIClock.FakeClock);
                    });
                });

            Server = builder.Start();
            Factory = new ArkFlurlClientFactory(new TestServerfactory(Server.GetTestServer()));

            var configuration = Server.Services.GetRequiredService<IConfiguration>();

            TestConfig = configuration.BuildApiHostConfig();
        }

        [BeforeFeature(Order = 0)]
        public static void BeforeFeature(FeatureContext ctx)
        {
            ctx.Set(Server);
            //ctx.Set(_client);
            //ctx.Set(_smtp);
        }

        [BeforeScenario(Order = 0)]
        public static void BeforeScenario(ScenarioContext ctx)
        {
            if (Factory == null) throw new InvalidOperationException("");
            ctx.ScenarioContainer.RegisterFactoryAs<IFlurlClient>(c => Factory.Get(_baseUri));
        }

        [AfterScenario]
        public static void FlushLogs()
        {
            try
            {
                LogManager.Flush(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }

        [AfterTestRun]
        public static void AfterTests()
        {
            _server?.Dispose();
        }

        public void Dispose()
        {
            try
            {
                _afterScenarioAssertionScope?.Dispose();
            }
            catch
            {
            }
        }
    }

    sealed class TestServerfactory : DefaultFlurlClientFactory
    {
        private readonly TestServer _server;

        public TestServerfactory(TestServer server)
        {
            _server = server;
        }

        public override HttpMessageHandler CreateInnerHandler()
        {
            return _server.CreateHandler();
        }
    }


    public class TestEnv
    {
        public TestEnv()
        {
            TestDataFilePath = Path.GetDirectoryName(AppContext.BaseDirectory) + @"\TestData\";
        }

        public ClaimsPrincipal TestPrincipal { get; } = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "Specflow")
            ], "SYSTEM"));

        public InMemNetwork RebusNetwork { get; } = new InMemNetwork(true);
        public InMemorySubscriberStore RebusSubscriber { get; } = new InMemorySubscriberStore();
        public string TestDataFilePath { get; }
    }
}
