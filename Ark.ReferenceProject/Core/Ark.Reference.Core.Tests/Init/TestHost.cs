using Ark.Tools.Http;
using Ark.Tools.Outbox;
using Ark.Tools.Rebus.Tests;

using Ark.Reference.Core.Application;
using Ark.Reference.Core.Application.Config;
using Ark.Reference.Core.WebInterface;

using FluentAssertions;

using Flurl;
using Flurl.Http;
using Flurl.Http.Configuration;

using Ark.Reference.Common;

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

using SimpleInjector;

using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Threading;

using TechTalk.SpecFlow;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Tests.Init
{
    [Binding]
    public sealed class TestHost : IDisposable
    {
        private const string _baseUri = "https://localhost:5001";

        public static ApiHostConfig TestConfig { get; private set; }
        public static ICoreDataContextConfig DBConfig => TestConfig;

        private static IHost _server;
        private static IFlurlClientCache _factory;
        public static readonly TestEnv Env = new TestEnv();

        private static ScenarioContext _scenarioContext;
        private FluentAssertions.Execution.AssertionScope _afterScenarioAssertionScope;

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
            _afterScenarioAssertionScope = new FluentAssertions.Execution.AssertionScope();
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
            _scenarioContext = null;
        }

        [Then("I wait background bus to idle and outbox to be empty")]
        [When("I wait background bus to idle and outbox to be empty")]
        public void ThenIWaitBackgroundBusToIdleAndOutboxToBeEmpty()
        {
            _backgroundBus(false);
        }


        [When("I wait background bus to idle and outbox to be empty ignoring scheduled message")]
        public void ThenIWaitBackgroundBusToIdleAndOutboxToBeEmptyIgnoringScheduledMessage()
        {
            _backgroundBus(true);
        }

        private void _backgroundBus(bool ignoreDeferred)
        {
            using var _ = new FluentAssertions.Execution.AssertionScope();

            var ctx = _server.Services.GetService<Container>().GetInstance<Func<IOutboxContext>>();

            var (inqueue, inprocess, deferred, outbox, errorMessages) =
                Policy
                    .HandleResult<(int inqueue, int inprocess, int deferred, int outbox, int errorMessages)>(
                        (c) =>
                        {
                            int def = c.deferred;
                            return c.errorMessages == 0 && (c.inqueue + c.inprocess + def + c.outbox) == 0;
                        }
                    ) // if true, go again
                    .WaitAndRetry(1, i => TimeSpan.FromMilliseconds(100))
                    .Wrap(
                        Policy
                            .HandleResult<(int inqueue, int inprocess, int deferred, int outbox, int errorMessages)>(
                                (c) =>
                                {
                                    int def = c.deferred;
                                    return c.errorMessages == 0 && (c.inqueue + c.inprocess + def + c.outbox) > 0; // if true, go again
                                }
                            )
                            .WaitAndRetry(600, _ => TimeSpan.FromMilliseconds(100))
                     )
                    .Execute(() =>
                    {
                        var inqueue = Env.RebusNetwork.Count();
                        var inprocess = InProcessMessageInspectorStep.Count;
                        var errorMessages = Env.RebusNetwork.Count("error");
                        var due = ignoreDeferred ? 0 : TestsInMemoryTimeoutManager.DueCount;


                        using var outbox = ctx();
                        var outboxCount = outbox.CountAsync().GetAwaiter().GetResult();
                        outbox.Commit();

                        return (inqueue, inprocess, due, outboxCount, errorMessages);
                    });

            errorMessages.Should().Be(0);
            inqueue.Should().Be(0);
            inprocess.Should().Be(0);
            deferred.Should().Be(0);
            outbox.Should().Be(0);
        }

        [AfterScenario(Order = int.MaxValue - 1)]
        public void ClearRebus()
        {
            using var drainer = DrainableInMemTransport.Drain();
            do
            {
                {
                    using var outbox = _server.Services.GetService<Container>().GetInstance<Func<IOutboxContext>>()();
                    outbox.ClearAsync().GetAwaiter().GetResult();
                    outbox.Commit();
                }
                TestsInMemoryTimeoutManager.ClearPendingDue();
                Env.RebusNetwork.Reset();

                while (InProcessMessageInspectorStep.Count > 0)
                    Thread.Sleep(100);

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

            var builder = Program.GetHostBuilder(Array.Empty<string>()) 
                .ConfigureWebHost(wh =>
                {
                    wh.UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddTransient<Func<ScenarioContext>>(s => () => _scenarioContext);
                        services.AddSingleton(Env.RebusNetwork);
                        services.AddSingleton(Env.RebusSubscriber);
                        services.AddSingleton<IClock>(MockIClock.FakeClock);
                    });
                });

            _server = builder.Start();
            _factory = new FlurlClientCache()
                .WithDefaults(builder =>
                {
                    builder.ConfigureArkDefaults();
                    builder.ConfigureHttpClient(c => c.BaseAddress = new Uri(_baseUri));
                    builder.AddMiddleware(() => new TestServerMessageHandler(_server.GetTestServer()));
                });

            var configuration = _server.Services.GetRequiredService<IConfiguration>();

            TestConfig = configuration.BuildApiHostConfig();
        }

        [BeforeFeature(Order = int.MinValue)]
        public static void BeforeFeature(FeatureContext ctx)
        {
            ctx.Set(_server);
        }

        [BeforeScenario(Order = 0)]
        public static void BeforeScenario(ScenarioContext ctx)
        {
            if (_factory == null) throw new InvalidOperationException("");

            ctx.ScenarioContainer.RegisterFactoryAs<IFlurlClient>(c => _factory.GetOrAdd(_baseUri, _baseUri));
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
            _factory?.Clear();
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

    public class TestServerMessageHandler(TestServer testServer) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            InnerHandler?.Dispose();
            InnerHandler = testServer.CreateHandler();
            return base.SendAsync(request, cancellationToken);
        }
    }


    public class TestEnv
    {
        public TestEnv()
        {
            var codeBase = Assembly.GetExecutingAssembly().Location;
            var uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            TestDataFilePath = Path.GetDirectoryName(path) + @"\TestData\";
        }

        public ClaimsPrincipal TestPrincipal { get; } = new ClaimsPrincipal(new ClaimsIdentity(new[]{
            new Claim(ClaimTypes.NameIdentifier, "Specflow")
            }, "SYSTEM"));

        public InMemNetwork RebusNetwork { get; } = new InMemNetwork(true);
        public InMemorySubscriberStore RebusSubscriber { get; } = new InMemorySubscriberStore();
        public string TestDataFilePath { get; }
    }
}
