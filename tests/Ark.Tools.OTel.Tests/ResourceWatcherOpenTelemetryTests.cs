// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Ark.Tools.ResourceWatcher;

using NodaTime;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Tests;

[TestClass]
public sealed class ResourceWatcherOpenTelemetryTests
{
    [TestMethod]
    public async Task RunOnce_ExportsStableOperationsAndPayloadAttributes()
    {
        var collector = new CollectingProcessor();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(ResourceWatcherInstrumentation.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(collector)
            .Build();
        using var watcher = new TestWatcher();

        await watcher.RunOnce().ConfigureAwait(false);

        collector.Spans.Should().Contain(x => x.OperationName == "Ark.Tools.ResourceWatcher.Run");
        collector.Spans.Should().Contain(x => x.OperationName == "Ark.Tools.ResourceWatcher.GetResources");
        collector.Spans.Should().Contain(x => x.OperationName == "Ark.Tools.ResourceWatcher.CheckState");
        collector.Spans.Should().Contain(x =>
            x.OperationName == "Ark.Tools.ResourceWatcher.ProcessResource"
            && x.GetTagItem("ResourceId")?.ToString() == "resource-1"
            && x.GetTagItem("ResultType")?.ToString() == nameof(ResultType.Normal));
        collector.Spans.Should().Contain(x =>
            x.OperationName == "Ark.Tools.ResourceWatcher.Run"
            && x.GetTagItem("Tenant")?.ToString() == "tenant");
    }

    private sealed class TestWatcher : ResourceWatcher<TestResource, VoidExtensions>
    {
        private static readonly LocalDateTime _modified = new(2026, 8, 16, 0, 0);

        public TestWatcher()
            : base(
                new TestConfig(),
                new InMemStateProvider<VoidExtensions>())
        {
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
