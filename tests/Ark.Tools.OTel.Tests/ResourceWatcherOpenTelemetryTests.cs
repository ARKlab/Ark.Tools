// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Ark.Tools.ResourceWatcher;

using NodaTime;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.Tools.OTel.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ResourceWatcherOpenTelemetryTests
{
    [TestMethod]
    public async Task RunOnce_ExportsStableOperationsAndPayloadAttributes()
    {
        using var collector = new CollectingProcessor();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ResourceWatcherInstrumentation.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(collector)
            .Build();
        using var watcher = new TestWatcher();

        await watcher.RunOnce().ConfigureAwait(false);

        collector.Spans.Should().Contain(x => x.OperationName == "ark.tools.resourcewatcher.run");
        collector.Spans.Should().Contain(x => x.OperationName == "ark.tools.resourcewatcher.get_resources");
        collector.Spans.Should().Contain(x => x.OperationName == "ark.tools.resourcewatcher.check_state");
        collector.Spans.Should().Contain(x =>
            x.OperationName == "ark.tools.resourcewatcher.process_resource"
            && _hasTag(x, "resource_id", "resource-1")
            && _hasTag(x, "result_type", nameof(ResultType.Normal)));
        collector.Spans.Should().Contain(x =>
            x.OperationName == "ark.tools.resourcewatcher.run"
            && _hasTag(x, "tenant", "tenant"));
    }

    [TestMethod]
    public async Task ProcessFailure_RecordsExceptionAndErrorStatus()
    {
        using var collector = new CollectingProcessor();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ResourceWatcherInstrumentation.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(collector)
            .Build();
        using var watcher = new TestWatcher(failProcessing: true);

        await watcher.RunOnce().ConfigureAwait(false);

        var activity = collector.Spans.Single(x => x.OperationName == "ark.tools.resourcewatcher.process_resource");
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.Events.Should().Contain(x => x.Name == "exception");
    }

    [TestMethod]
    public async Task RunOnce_RecordsRunAndResourceMetrics()
    {
        var measurements = new List<(string Name, long Value, string? Outcome)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ResourceWatcherInstrumentation.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            string? outcome = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                    outcome = tag.Value?.ToString();
            }

            measurements.Add((
                instrument.Name,
                value,
                outcome));
        });
        listener.Start();
        using var watcher = new TestWatcher();

        await watcher.RunOnce().ConfigureAwait(false);
        listener.RecordObservableInstruments();

        measurements.Should().Contain(x =>
            x.Name == "ark.tools.resourcewatcher.runs"
            && x.Value == 1
            && x.Outcome == "success");
        measurements.Should().Contain(x => x.Name == "ark.tools.resourcewatcher.resources.listed" && x.Value == 1);
        measurements.Should().Contain(x =>
            x.Name == "ark.tools.resourcewatcher.resources.processed"
            && x.Value == 1
            && x.Outcome == "success");
    }

    private static bool _hasTag(Activity activity, string name, string expectedValue)
    {
        return activity.GetTagItem(name)?.ToString() == expectedValue;
    }

    private sealed class TestWatcher : ResourceWatcher<TestResource, VoidExtensions>
    {
        private static readonly LocalDateTime _modified = new(2026, 8, 16, 0, 0);
        private readonly bool _failProcessing;

        public TestWatcher(bool failProcessing = false)
            : base(
                new TestConfig(),
                new InMemStateProvider<VoidExtensions>())
        {
            _failProcessing = failProcessing;
        }

        protected override Task<IEnumerable<IResourceMetadata<VoidExtensions>>> _getResourcesInfo(CancellationToken ctk = default)
        {
            return Task.FromResult<IEnumerable<IResourceMetadata<VoidExtensions>>>([
                new TestMetadata
                {
                    ResourceId = "resource-1",
                    Modified = _modified,
                    Extensions = VoidExtensions.Instance
                }
            ]);
        }

        protected override Task<TestResource?> _retrievePayload(
            IResourceMetadata<VoidExtensions> info,
            IResourceTrackedState<VoidExtensions>? lastState,
            CancellationToken ctk = default)
        {
            return Task.FromResult<TestResource?>(new TestResource());
        }

        protected override async Task _processResource(
            ChangedStateContext<TestResource, VoidExtensions> context,
            CancellationToken ctk = default)
        {
            if (_failProcessing)
                throw new InvalidOperationException("test failure");

            _ = await context.Payload.ConfigureAwait(false);
        }
    }

    private sealed class TestResource : IResourceState
    {
        public Instant RetrievedAt => SystemClock.Instance.GetCurrentInstant();

        public string? CheckSum => "checksum";
    }

    private sealed class TestMetadata : IResourceMetadata<VoidExtensions>
    {
        public required string ResourceId { get; init; }

        public LocalDateTime Modified { get; init; }

        public VoidExtensions? Extensions { get; init; }
    }

    private sealed class TestConfig : IResourceWatcherConfig
    {
        public string Tenant => "tenant";

        public int SleepSeconds => 1;

        public int MaxRetries => 3;

        public uint DegreeOfParallelism => 1;

        public uint? SkipResourcesOlderThanDays => null;

        public bool IgnoreState => true;

        public Duration BanDuration => Duration.FromMinutes(1);

        public TimeSpan RunDurationNotificationLimit => TimeSpan.MaxValue;

        public TimeSpan ResourceDurationNotificationLimit => TimeSpan.MaxValue;
    }
}
