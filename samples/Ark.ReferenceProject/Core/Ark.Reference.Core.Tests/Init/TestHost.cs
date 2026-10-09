using Ark.Reference.Core.Application.Config;
using Ark.Reference.Core.InProcessHost;
using Ark.Tools.Http;
using Ark.Tools.Outbox;
using Ark.Tools.OTel;
using Ark.Tools.Rebus.Tests;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NLog;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Reqnroll;

using SimpleInjector;

[assembly: DoNotParallelize]

namespace Ark.Reference.Core.Tests.Init;

[Binding]
public sealed class TestHost : IDisposable
{
    private static readonly Uri _baseUri = new("https://localhost:5001");

    public static ApiHostConfig TestConfig => ReferenceTestHost.Config;
    public static ICoreDataContextConfig DBConfig => TestConfig;

    public static IHost Server => ReferenceTestHost.Server;
    public static ArkFlurlClientFactory Factory => ReferenceTestHost.Factory;
    internal static readonly OtelTestCollector _telemetry = new();
    internal static ComplianceLogCollector _logs = null!;

    private static ArkTelemetryFileCollector? _fileTelemetry;
    public static TestEnv Env => ReferenceTestHost.Env;

    private static ScenarioContext? _scenarioContext;
    private AwesomeAssertions.Execution.AssertionScope? _afterScenarioAssertionScope;

    [BeforeScenario(Order = 0)]
    public static void Set(ScenarioContext ctx)
    {
        _scenarioContext = ctx;
        _telemetry._reset();
        _logs._reset();
    }

    [AfterScenario(Order = int.MinValue)]
    public void EnsureAllHooksRun()
    {
        // Reqnroll: If a hook throws an unhandled exception, subsequent hooks of the same type are not executed.
        // Reqnroll: If you want to ensure that all hooks of the same types are executed, you need to handle your exceptions manually.
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
#pragma warning disable ERP022 // Intentional cleanup - exceptions ignored
        catch
        {
            // Best-effort teardown: a failure here must not prevent the remaining hooks from running.
        }
#pragma warning restore ERP022
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

    private static async Task _backgroundBus(bool ignoreDeferred)
    {
        using var _ = new AwesomeAssertions.Execution.AssertionScope();

        var ctx = Server.Services.GetRequiredService<Container>().GetInstance<IOutboxAsyncContextFactory>();

        // Rebus can briefly report no work between outbox dequeue and message dispatch: require consecutive idle samples
        const int RequiredIdleSamples = 5;
        var idleSamples = 0;
        (int inqueue, int inprocess, int deferred, int outbox, int errorMessages) counts = default;
        for (var poll = 0; poll < 600 && idleSamples < RequiredIdleSamples; poll++)
        {
            if (poll > 0)
                await Task.Delay(100).ConfigureAwait(false);

            counts = await _sampleBusAsync(ctx, ignoreDeferred).ConfigureAwait(false);
            if (counts.errorMessages > 0)
                break;

            idleSamples = counts.inqueue + counts.inprocess + counts.deferred + counts.outbox == 0 ? idleSamples + 1 : 0;
        }

        var (inqueue, inprocess, deferred, outbox, errorMessages) = counts;
        errorMessages.Should().Be(0);
        inqueue.Should().Be(0);
        inprocess.Should().Be(0);
        deferred.Should().Be(0);
        outbox.Should().Be(0);
        idleSamples.Should().Be(RequiredIdleSamples, "the bus must stay idle for consecutive samples");

        _flushTelemetry();
    }

    private static async Task<(int inqueue, int inprocess, int deferred, int outbox, int errorMessages)> _sampleBusAsync(
        IOutboxAsyncContextFactory ctx,
        bool ignoreDeferred)
    {
        var inqueue = Env.RebusNetwork.Count();
        var inprocess = InProcessMessageInspectorStep.Count;
        var errorMessages = Env.RebusNetwork.Count("error");
        var due = ignoreDeferred ? 0 : TestsInMemoryTimeoutManager.DueCount;

        var outbox = await ctx.CreateAsync().ConfigureAwait(false);
        await using var _ = outbox.ConfigureAwait(false);
        var outboxCount = await outbox.CountAsync().ConfigureAwait(false);
        await outbox.CommitAsync().ConfigureAwait(false);

        return (inqueue, inprocess, due, outboxCount, errorMessages);
    }

    private static void _flushTelemetry()
    {
        var tracerProvider = Server.Services.GetService<TracerProvider>();
        tracerProvider?.ForceFlush();

        var meterProvider = Server.Services.GetService<MeterProvider>();
        meterProvider?.ForceFlush();
    }

    [AfterScenario(Order = int.MaxValue - 1)]
    public async Task ClearRebus()
    {
        using var drainer = DrainableInMemTransport.Drain();
        do
        {
            {
                var outbox = await Server.Services.GetRequiredService<Container>().GetInstance<IOutboxAsyncContextFactory>().CreateAsync().ConfigureAwait(false);
                await using var _ = outbox.ConfigureAwait(false);
                await outbox.ClearAsync().ConfigureAwait(false);
                await outbox.CommitAsync().ConfigureAwait(false);
            }
            TestsInMemoryTimeoutManager.ClearPendingDue();
            Env.RebusNetwork.Reset();

            while (InProcessMessageInspectorStep.Count > 0)
                await Task.Delay(100).ConfigureAwait(false);

        } while (drainer.StillDraining);

    }

    [BeforeTestRun(Order = 0)]
    public static void BeforeTests0()
    {
        ReferenceTestHost.Initialize();
    }

    [BeforeTestRun]
    public static void BeforeTests()
    {
        //OutboxMessageConstants.OutboxWaitTimeSpan = TimeSpan.FromMilliseconds(50);

        //ApplicationConstants.ComputeIdleDetectWindow = TimeSpan.FromSeconds(1);

        _fileTelemetry = ArkTelemetryFileCollector.StartFromEnvironment();
        ReferenceTestHost.Start(static services =>
        {
            services.AddTransient<Func<ScenarioContext>>(static s => static () => _scenarioContext ?? throw new InvalidOperationException("ScenarioContext is accessed outside of a Scenario."));
            services.AddSingleton(MockIClock.FakeClock);
        });
        _logs = new ComplianceLogCollector();
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
        ctx.ScenarioContainer.RegisterFactoryAs(static c => Factory.Get(_baseUri));
    }

    [AfterScenario]
    public static void FlushLogs()
    {
        try
        {
            LogManager.Flush(TimeSpan.FromSeconds(2));
        }
#pragma warning disable ERP022 // Intentional cleanup - exceptions ignored
        catch
        {
            // Best-effort teardown: a failure here must not prevent the remaining hooks from running.
        }
#pragma warning restore ERP022
    }

    [AfterTestRun]
    public static void AfterTests()
    {
        ReferenceTestHost.Stop();
        _logs.Dispose();
        _fileTelemetry?.Dispose();
        _telemetry.Dispose();
    }

    public void Dispose()
    {
        try
        {
            _afterScenarioAssertionScope?.Dispose();
        }
#pragma warning disable ERP022 // Intentional cleanup - exceptions ignored
        catch
        {
            // Best-effort teardown: a failure here must not prevent the remaining hooks from running.
        }
#pragma warning restore ERP022
    }
}
