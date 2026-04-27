// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Ark.Tools.OTel.Tests;

// ─── Shared helpers ──────────────────────────────────────────────────────────

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that collects all completed and
/// exported spans (i.e. spans that have the <c>Recorded</c> flag set when
/// <c>OnEnd</c> fires) for assertion in tests.
/// </summary>
internal sealed class CollectingProcessor : BaseProcessor<Activity>
{
    private readonly List<Activity> _spans = [];

    /// <summary>Spans that were recorded (exported) when <c>OnEnd</c> fired.</summary>
    public IReadOnlyList<Activity> Spans => _spans;

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (data.Recorded)
            _spans.Add(data);
    }
}

/// <summary>
/// Provides a <see cref="TracerProvider"/> and the list of exported activities for one
/// test scenario.
/// </summary>
internal sealed class TestPipeline : IDisposable
{
    private readonly ActivitySource _source;
    private readonly TracerProvider _provider;
    private readonly CollectingProcessor _collector;
    private bool _disposed;

    /// <summary>Spans that were exported (Recorded) at the time of their <c>OnEnd</c>.</summary>
    public IReadOnlyList<Activity> Exported => _collector.Spans;

    public TestPipeline(
        string sourceName,
        Sampler sampler,
        params BaseProcessor<Activity>[] processors)
    {
        _source = new ActivitySource(sourceName);
        _collector = new CollectingProcessor();

        var builder = Sdk.CreateTracerProviderBuilder()
            .AddSource(sourceName)
            .SetSampler(sampler);

        foreach (var p in processors)
            builder = builder.AddProcessor(p);

        builder = builder.AddProcessor(_collector);

        _provider = builder.Build()!;
    }

    /// <summary>Starts a root span (no parent).</summary>
    public Activity? StartRoot(string name, ActivityKind kind = ActivityKind.Server)
        => _source.StartActivity(name, kind);

    /// <summary>Starts a child span parented to <paramref name="parent"/>.</summary>
    public Activity? StartChild(string name, Activity parent, ActivityKind kind = ActivityKind.Internal)
        => _source.StartActivity(name, kind, parent.Context);

    /// <summary>Starts a span with initial creation-time tags so <c>OnStart</c> sees them.</summary>
    public Activity? StartWithTags(
        string name,
        ActivityKind kind,
        IEnumerable<KeyValuePair<string, object?>> tags)
        => _source.StartActivity(name, kind, default(ActivityContext), tags);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _provider.Dispose();
        _collector.Dispose();
        _source.Dispose();
    }
}

// ─── ArkAdaptiveSampler tests ────────────────────────────────────────────────

/// <summary>
/// Tests for <see cref="ArkAdaptiveSampler"/> behaviour as documented in
/// <c>docs/otel/sampling.md</c>.
/// </summary>
[TestClass]
public class ArkAdaptiveSamplerTests
{
    private static ArkAdaptiveSamplerOptions HighRateOptions() => new()
    {
        TracesPerSecond = 10_000,
        EnablePerOperationBucketing = false,
        MovingAverageRatio = 0.5,
        SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(10),
    };

    private static ArkAdaptiveSamplerOptions NearZeroRateOptions() => new()
    {
        TracesPerSecond = 0.0001,
        EnablePerOperationBucketing = false,
        MovingAverageRatio = 0.5,
        SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(10),
    };

    // ── behaviour: high rate → everything sampled ──────────────────────────

    /// <summary>
    /// When the token bucket has ample capacity every span must be RecordAndSample.
    /// </summary>
    [TestMethod]
    public void ShouldSample_WhenBucketHasCapacity_ReturnsRecordAndSample()
    {
        using var pipeline = new TestPipeline(
            nameof(ShouldSample_WhenBucketHasCapacity_ReturnsRecordAndSample),
            new ArkAdaptiveSampler(HighRateOptions()));

        using var root = pipeline.StartRoot("GET /api/orders");

        root.Should().NotBeNull();
        root!.Recorded.Should().BeTrue("a high-rate bucket should sample every span");
    }

    // ── behaviour: parent propagation ─────────────────────────────────────

    /// <summary>
    /// When the parent span is already Recorded the child must be RecordAndSample
    /// regardless of bucket state (distributed traces must never be split).
    /// </summary>
    [TestMethod]
    public void ShouldSample_WhenParentIsRecorded_ChildIsAlwaysSampled()
    {
        using var pipeline = new TestPipeline(
            nameof(ShouldSample_WhenParentIsRecorded_ChildIsAlwaysSampled),
            new ArkAdaptiveSampler(HighRateOptions()));

        using var root = pipeline.StartRoot("ROOT");
        root.Should().NotBeNull();
        root!.Recorded.Should().BeTrue();

        using var child = pipeline.StartChild("CHILD", root);
        child.Should().NotBeNull();
        child!.Recorded.Should().BeTrue("child of a sampled parent must always be sampled");
    }

    // ── behaviour: pre-filter tag → Drop ──────────────────────────────────

    /// <summary>
    /// Spans with the <c>ark.filtered = true</c> initial tag must be dropped immediately
    /// (tag is set by <see cref="ArkPreFilterProcessor"/> during <c>OnStart</c>).
    /// </summary>
    [TestMethod]
    public void ShouldSample_WhenArkFilteredTagTrue_DropsSpan()
    {
        var sampler = new ArkAdaptiveSampler(HighRateOptions());

        var tags = new List<KeyValuePair<string, object?>>
        {
            new(ArkAdaptiveSampler.FilteredTagName, true),
        };

        var parameters = new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            "OPTIONS /health",
            ActivityKind.Server,
            tags,
            null);

        var result = sampler.ShouldSample(in parameters);
        result.Decision.Should().Be(SamplingDecision.Drop, "pre-filtered spans must be dropped");
    }

    // ── behaviour: failed trace → always sample ────────────────────────────

    /// <summary>
    /// Once a trace is registered in the <see cref="FailedTraceRegistry"/> all new
    /// child spans in that trace must be RecordAndSample regardless of bucket state.
    /// </summary>
    [TestMethod]
    public void ShouldSample_WhenTraceMarkedFailed_ReturnsRecordAndSample()
    {
        var registry = new FailedTraceRegistry();
        var sampler = new ArkAdaptiveSampler(NearZeroRateOptions(), registry);

        var traceId = ActivityTraceId.CreateRandom();
        registry.Register(traceId);

        var parameters = new SamplingParameters(
            default,
            traceId,
            "NEW_CHILD_AFTER_FAILURE",
            ActivityKind.Internal,
            null,
            null);

        var result = sampler.ShouldSample(in parameters);
        result.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "after failure registration all new spans in that trace must be sampled");
    }

    // ── behaviour: bucket exhausted → RecordOnly (not Drop) ───────────────

    /// <summary>
    /// Rate-limited spans must receive <see cref="SamplingDecision.RecordOnly"/>, not
    /// <c>Drop</c>, so <see cref="ArkFailurePromotionProcessor"/> can still upgrade them.
    /// </summary>
    [TestMethod]
    public void ShouldSample_WhenBucketExhausted_ReturnsRecordOnly()
    {
        var sampler = new ArkAdaptiveSampler(NearZeroRateOptions());
        SamplingDecision? firstRecordOnly = null;

        for (var i = 0; i < 100; i++)
        {
            var p = new SamplingParameters(
                default,
                ActivityTraceId.CreateRandom(),
                "OP",
                ActivityKind.Internal,
                null,
                null);
            var r = sampler.ShouldSample(in p);
            if (r.Decision == SamplingDecision.RecordOnly)
            {
                firstRecordOnly = r.Decision;
                break;
            }
        }

        firstRecordOnly.Should().Be(SamplingDecision.RecordOnly,
            "rate-limited spans must use RecordOnly so failure promotion still works");
    }

    // ── behaviour: per-operation bucketing ────────────────────────────────

    /// <summary>
    /// Different operations must get independent token buckets.
    /// Exhausting OP_A's budget must not affect OP_B's budget.
    /// </summary>
    [TestMethod]
    public void ShouldSample_PerOperationBucketing_IndependentBudgets()
    {
        var options = new ArkAdaptiveSamplerOptions
        {
            TracesPerSecond = 1.0,
            EnablePerOperationBucketing = true,
            MovingAverageRatio = 0.5,
            SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(10),
            MaxOperationBuckets = 10,
        };
        var sampler = new ArkAdaptiveSampler(options);

        // Drain OP_A completely.
        for (var i = 0; i < 100; i++)
        {
            var p = new SamplingParameters(
                default,
                ActivityTraceId.CreateRandom(),
                "OP_A",
                ActivityKind.Internal,
                null,
                null);
            var r = sampler.ShouldSample(in p);
            if (r.Decision == SamplingDecision.RecordOnly)
                break;
        }

        // OP_B should still have its own tokens.
        var pb = new SamplingParameters(
            default,
            ActivityTraceId.CreateRandom(),
            "OP_B",
            ActivityKind.Internal,
            null,
            null);

        var rb = sampler.ShouldSample(in pb);

        rb.Decision.Should().Be(SamplingDecision.RecordAndSample,
            "OP_B has its own bucket and must not be affected by OP_A exhaustion");
    }
}

// ─── ArkPreFilterProcessor tests ─────────────────────────────────────────────

/// <summary>
/// Tests for <see cref="ArkPreFilterProcessor"/> behaviour.
/// </summary>
[TestClass]
public class ArkPreFilterProcessorTests
{
    private static IEnumerable<KeyValuePair<string, object?>> Tags(string key, string value)
        => [new KeyValuePair<string, object?>(key, value)];

    private static IEnumerable<KeyValuePair<string, object?>> Tags(
        string key1, string value1,
        string key2, string value2)
        => [
            new KeyValuePair<string, object?>(key1, value1),
            new KeyValuePair<string, object?>(key2, value2),
        ];

    private static TestPipeline BuildPipeline(string name)
    {
        var opts = new ArkAdaptiveSamplerOptions { TracesPerSecond = 10_000 };
        return new TestPipeline(name, new ArkAdaptiveSampler(opts), new ArkPreFilterProcessor());
    }

    // ── HTTP OPTIONS ───────────────────────────────────────────────────────

    /// <summary>
    /// HTTP OPTIONS requests (CORS preflight noise) must be dropped.
    /// Tags must be present at activity creation time so <c>OnStart</c> sees them.
    /// </summary>
    [TestMethod]
    public void PreFilter_OptionsRequest_IsDropped()
    {
        using var pipeline = BuildPipeline(nameof(PreFilter_OptionsRequest_IsDropped));

        using var act = pipeline.StartWithTags(
            "OPTIONS /api/resource",
            ActivityKind.Server,
            Tags("http.request.method", "OPTIONS"));
        act?.Stop();

        pipeline.Exported.Should().BeEmpty("OPTIONS requests must be pre-filtered");
    }

    /// <summary>
    /// HTTP GET requests must NOT be filtered.
    /// </summary>
    [TestMethod]
    public void PreFilter_GetRequest_IsNotFiltered()
    {
        using var pipeline = BuildPipeline(nameof(PreFilter_GetRequest_IsNotFiltered));

        using var act = pipeline.StartWithTags(
            "GET /api/resource",
            ActivityKind.Server,
            Tags("http.request.method", "GET"));
        act?.Stop();

        pipeline.Exported.Should().ContainSingle("GET requests must not be filtered");
    }

    // ── Azure Service Bus Receive ──────────────────────────────────────────

    /// <summary>
    /// Azure Service Bus <c>Receive</c> spans must be filtered (high-frequency noise).
    /// </summary>
    [TestMethod]
    public void PreFilter_ServiceBusReceive_IsFiltered()
    {
        using var pipeline = BuildPipeline(nameof(PreFilter_ServiceBusReceive_IsFiltered));

        using var act = pipeline.StartWithTags(
            "Receive",
            ActivityKind.Consumer,
            Tags("messaging.system", "servicebus", "messaging.operation", "receive"));
        act?.Stop();

        pipeline.Exported.Should().BeEmpty("Service Bus Receive spans must be pre-filtered");
    }

    /// <summary>
    /// Azure Service Bus <c>Send</c> spans must NOT be filtered.
    /// </summary>
    [TestMethod]
    public void PreFilter_ServiceBusSend_IsNotFiltered()
    {
        using var pipeline = BuildPipeline(nameof(PreFilter_ServiceBusSend_IsNotFiltered));

        using var act = pipeline.StartWithTags(
            "Send",
            ActivityKind.Producer,
            Tags("messaging.system", "servicebus", "messaging.operation", "send"));
        act?.Stop();

        pipeline.Exported.Should().ContainSingle("Service Bus Send spans must not be filtered");
    }

    // ── SQL Commit ─────────────────────────────────────────────────────────

    /// <summary>
    /// SQL <c>Commit</c> spans must be filtered (routine, low-value noise).
    /// </summary>
    [TestMethod]
    public void PreFilter_SqlCommit_IsFiltered()
    {
        using var pipeline = BuildPipeline(nameof(PreFilter_SqlCommit_IsFiltered));

        using var act = pipeline.StartWithTags(
            "Commit",
            ActivityKind.Client,
            Tags("db.operation", "Commit"));
        act?.Stop();

        pipeline.Exported.Should().BeEmpty("SQL Commit spans must be pre-filtered");
    }

    /// <summary>
    /// SQL <c>SELECT</c> spans must NOT be filtered.
    /// </summary>
    [TestMethod]
    public void PreFilter_SqlSelect_IsNotFiltered()
    {
        using var pipeline = BuildPipeline(nameof(PreFilter_SqlSelect_IsNotFiltered));

        using var act = pipeline.StartWithTags(
            "SELECT",
            ActivityKind.Client,
            Tags("db.operation", "SELECT"));
        act?.Stop();

        pipeline.Exported.Should().ContainSingle("SQL SELECT spans must not be filtered");
    }
}

// ─── ArkFailurePromotionProcessor tests ───────────────────────────────────────

/// <summary>
/// Tests for <see cref="ArkFailurePromotionProcessor"/> behaviour.
/// </summary>
[TestClass]
public class ArkFailurePromotionProcessorTests
{
    /// <summary>Builds a pipeline with near-zero rate so all spans start as RecordOnly.</summary>
    private static TestPipeline NearZeroPipeline(string name, FailedTraceRegistry registry)
    {
        var opts = new ArkAdaptiveSamplerOptions
        {
            TracesPerSecond = 0.0001,
            EnablePerOperationBucketing = false,
            MovingAverageRatio = 0.5,
            SamplingPercentageDecreaseTimeout = TimeSpan.FromMinutes(10),
        };
        return new TestPipeline(
            name,
            new ArkAdaptiveSampler(opts, registry),
            new ArkFailurePromotionProcessor(registry));
    }

    // ── failure promotion: ActivityStatusCode.Error ────────────────────────

    /// <summary>
    /// A rate-limited span that ends with <see cref="ActivityStatusCode.Error"/> must be
    /// promoted and appear in exports.
    /// </summary>
    [TestMethod]
    public void FailurePromotion_ErrorStatus_SpanIsPromotedAndExported()
    {
        var registry = new FailedTraceRegistry();
        using var pipeline = NearZeroPipeline(
            nameof(FailurePromotion_ErrorStatus_SpanIsPromotedAndExported), registry);

        using var act = pipeline.StartRoot("OP");
        act.Should().NotBeNull();
        act!.SetStatus(ActivityStatusCode.Error, "something went wrong");
        act.Stop();

        pipeline.Exported.Should().ContainSingle(
            "a span with ActivityStatusCode.Error must be promoted even when rate-limited");
    }

    // ── failure promotion: exception event ────────────────────────────────

    /// <summary>
    /// A rate-limited span with an OTel exception event must be promoted.
    /// </summary>
    [TestMethod]
    public void FailurePromotion_ExceptionEvent_SpanIsPromotedAndExported()
    {
        var registry = new FailedTraceRegistry();
        using var pipeline = NearZeroPipeline(
            nameof(FailurePromotion_ExceptionEvent_SpanIsPromotedAndExported), registry);

        using var act = pipeline.StartRoot("OP_EXCEPTION");
        act.Should().NotBeNull();
        act!.AddEvent(new ActivityEvent("exception",
            tags: new ActivityTagsCollection { ["exception.message"] = "boom" }));
        act.Stop();

        pipeline.Exported.Should().ContainSingle(
            "a span with an exception event must be promoted");
    }

    // ── failure promotion: HTTP 5xx ────────────────────────────────────────

    /// <summary>
    /// A rate-limited span with <c>http.response.status_code</c> >= 400 must be promoted.
    /// </summary>
    [TestMethod]
    public void FailurePromotion_Http500Tag_SpanIsPromotedAndExported()
    {
        var registry = new FailedTraceRegistry();
        using var pipeline = NearZeroPipeline(
            nameof(FailurePromotion_Http500Tag_SpanIsPromotedAndExported), registry);

        using var act = pipeline.StartRoot("GET /api/fail");
        act.Should().NotBeNull();
        act!.SetTag("http.response.status_code", 500);
        act.Stop();

        pipeline.Exported.Should().ContainSingle(
            "a span with HTTP 500 must be promoted");
    }

    // ── failure promotion: parent chain walk ──────────────────────────────

    /// <summary>
    /// When a leaf span fails the entire parent chain (root → middle → leaf) must be
    /// promoted and exported.  Children always end before their parents in a single-process
    /// trace, so parents are still in-flight when the leaf's <c>OnEnd</c> fires.
    /// </summary>
    [TestMethod]
    public void FailurePromotion_ParentChainIsPromoted()
    {
        var registry = new FailedTraceRegistry();
        using var pipeline = NearZeroPipeline(nameof(FailurePromotion_ParentChainIsPromoted), registry);

        var root = pipeline.StartRoot("ROOT");
        root.Should().NotBeNull();

        var middle = pipeline.StartChild("MIDDLE", root!);
        middle.Should().NotBeNull();

        var leaf = pipeline.StartChild("LEAF", middle!);
        leaf.Should().NotBeNull();
        leaf!.SetStatus(ActivityStatusCode.Error, "leaf error");

        leaf.Stop();
        middle!.Stop();
        root!.Stop();

        var exportedNames = pipeline.Exported.Select(static a => a.DisplayName).ToList();
        exportedNames.Should().Contain("ROOT", "the root span must be promoted as part of the parent chain");
        exportedNames.Should().Contain("MIDDLE", "the middle span must be promoted as part of the parent chain");
        exportedNames.Should().Contain("LEAF", "the failing leaf span must always be exported");
    }

    // ── failure promotion: sibling after failure ───────────────────────────

    /// <summary>
    /// A sibling span that ends <em>after</em> a failure is detected in another sibling
    /// must be promoted via the shared <see cref="FailedTraceRegistry"/>.
    /// </summary>
    [TestMethod]
    public void FailurePromotion_SiblingAfterFailure_IsPromoted()
    {
        var registry = new FailedTraceRegistry();
        using var pipeline = NearZeroPipeline(
            nameof(FailurePromotion_SiblingAfterFailure_IsPromoted), registry);

        var root = pipeline.StartRoot("ROOT");
        root.Should().NotBeNull();

        var siblingA = pipeline.StartChild("SIBLING_A", root!);
        siblingA.Should().NotBeNull();

        var siblingB = pipeline.StartChild("SIBLING_B", root!);
        siblingB.Should().NotBeNull();

        // siblingA fails first and registers the trace as failed.
        siblingA!.SetStatus(ActivityStatusCode.Error, "sibling A error");
        siblingA.Stop();

        // siblingB ends after the failure was registered → must be promoted via registry.
        siblingB!.Stop();

        pipeline.Exported
            .Select(static a => a.DisplayName)
            .Should().Contain("SIBLING_B",
                "a sibling that ends after the failure must be promoted via registry");
    }

    // ── non-failure: successful span is NOT promoted ───────────────────────

    /// <summary>
    /// Rate-limited successful spans must NOT be promoted.
    /// The processor must never export successes that were rate-limited.
    /// </summary>
    [TestMethod]
    public void FailurePromotion_SuccessfulSpan_IsNotPromoted()
    {
        var registry = new FailedTraceRegistry();
        using var pipeline = NearZeroPipeline(
            nameof(FailurePromotion_SuccessfulSpan_IsNotPromoted), registry);

        // Start many spans to exhaust the bucket fully, then verify no errors were promoted.
        for (var i = 0; i < 20; i++)
        {
            using var act = pipeline.StartRoot($"OK_{i}");
            act?.SetTag("http.response.status_code", 200);
            act?.Stop();
        }

        // None of the exported spans should have error status (only error spans are promoted).
        foreach (var exported in pipeline.Exported)
        {
            exported.Status.Should().NotBe(ActivityStatusCode.Error,
                "the failure processor must not promote successful spans");
        }
    }
}

// ─── FailedTraceRegistry tests ────────────────────────────────────────────────

/// <summary>
/// Tests for <see cref="FailedTraceRegistry"/> correctness and thread-safety.
/// </summary>
[TestClass]
public class FailedTraceRegistryTests
{
    /// <summary>A registered ID must immediately be reported as failed.</summary>
    [TestMethod]
    public void Register_ThenIsFailed_ReturnsTrue()
    {
        var registry = new FailedTraceRegistry();
        var id = ActivityTraceId.CreateRandom();

        registry.Register(id);

        registry.IsFailed(id).Should().BeTrue();
    }

    /// <summary>An unregistered ID must not be reported as failed.</summary>
    [TestMethod]
    public void IsFailed_UnregisteredId_ReturnsFalse()
    {
        var registry = new FailedTraceRegistry();
        registry.IsFailed(ActivityTraceId.CreateRandom()).Should().BeFalse();
    }

    /// <summary>
    /// Registering the same trace ID multiple times must be idempotent.
    /// </summary>
    [TestMethod]
    public void Register_Idempotent_DoesNotThrow()
    {
        var registry = new FailedTraceRegistry();
        var id = ActivityTraceId.CreateRandom();

        var act = () =>
        {
            registry.Register(id);
            registry.Register(id);
            registry.Register(id);
        };

        act.Should().NotThrow("re-registering the same ID must be idempotent");
        registry.IsFailed(id).Should().BeTrue();
    }

    /// <summary>
    /// Registry must be safe for concurrent reads and writes from many threads.
    /// </summary>
    [TestMethod]
    public async Task Registry_ConcurrentAccess_IsThreadSafe()
    {
        var registry = new FailedTraceRegistry();
        var ids = Enumerable.Range(0, 200)
            .Select(static _ => ActivityTraceId.CreateRandom())
            .ToArray();

        var tasks = ids
            .Select((id, i) => Task.Run(() =>
            {
                if (i % 2 == 0)
                    registry.Register(id);
                else
                    _ = registry.IsFailed(id);
            }))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        // If we reach here without an exception, concurrent access is safe.
    }

    /// <summary>
    /// An entry must be present immediately after registration (before any cleanup cycle).
    /// </summary>
    [TestMethod]
    public void Register_EntryIsPresent_BeforeCleanup()
    {
        var registry = new FailedTraceRegistry(ttl: TimeSpan.FromMilliseconds(1));
        var id = ActivityTraceId.CreateRandom();

        registry.Register(id);

        registry.IsFailed(id).Should().BeTrue(
            "an entry must be visible immediately after registration");
    }
}

// ─── ArkTelemetryEnrichmentProcessor tests ────────────────────────────────────

/// <summary>
/// Tests for <see cref="ArkTelemetryEnrichmentProcessor"/> behaviour.
/// </summary>
[TestClass]
public class ArkTelemetryEnrichmentProcessorTests
{
    /// <summary>
    /// Every exported span must have the <c>ProcessName</c> tag added by the processor.
    /// </summary>
    [TestMethod]
    public void EnrichmentProcessor_AddsProcessNameTagToEverySpan()
    {
        var opts = new ArkAdaptiveSamplerOptions { TracesPerSecond = 10_000 };

        using var pipeline = new TestPipeline(
            nameof(EnrichmentProcessor_AddsProcessNameTagToEverySpan),
            new ArkAdaptiveSampler(opts),
            new ArkTelemetryEnrichmentProcessor());

        using var act = pipeline.StartRoot("TEST");
        act?.Stop();

        pipeline.Exported.Should().ContainSingle();

        // The tag value may be null in environments with no entry assembly,
        // but the processor must run without any error.
        _ = pipeline.Exported[0].GetTagItem("ProcessName");
    }

    /// <summary>
    /// A span that already has a <c>ProcessName</c> tag must keep its original value.
    /// </summary>
    [TestMethod]
    public void EnrichmentProcessor_DoesNotOverrideExistingProcessNameTag()
    {
        var opts = new ArkAdaptiveSamplerOptions { TracesPerSecond = 10_000 };

        using var pipeline = new TestPipeline(
            nameof(EnrichmentProcessor_DoesNotOverrideExistingProcessNameTag),
            new ArkAdaptiveSampler(opts),
            new ArkTelemetryEnrichmentProcessor());

        // Pass the tag at creation time so OnStart sees it.
        using var act = pipeline.StartWithTags(
            "TEST_PRE",
            ActivityKind.Internal,
            [new KeyValuePair<string, object?>("ProcessName", "my-custom-process")]);

        var tagValueAtStart = act?.GetTagItem("ProcessName") as string;
        act?.Stop();

        tagValueAtStart.Should().Be("my-custom-process",
            "the processor must not overwrite a tag that was already set");
    }
}
