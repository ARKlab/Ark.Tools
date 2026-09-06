// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.MediatorFramework.Messaging.OTel;

using AwesomeAssertions;

using OpenTelemetry.Metrics;

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the two-tier messaging metric contract, its gate and its attribute budget.</summary>
[TestClass]
public sealed class MessagingMetricsTests
{
    private static readonly string[] _allowedTagKeys =
    [
        "messaging.system",
        "messaging.destination.name",
        "ark.participant",
        "outcome",
        "reason"
    ];

    [TestMethod]
    public void OperationalInstrumentsExposeTheStableContract()
    {
        var instruments = _publishedInstruments(OpenTelemetryProcessingMetricsStep.MeterName);

        instruments[MessagingMetrics.ConcurrencyLimitName]
            .Should().Be(("UpDownCounter`1", "{worker}", "Current concurrency limit of the processor host."));
        instruments[MessagingMetrics.InFlightName]
            .Should().Be(("UpDownCounter`1", "{message}", "Deliveries currently being dispatched to handlers."));
        instruments[MessagingMetrics.BufferedName]
            .Should().Be(("UpDownCounter`1", "{message}", "Deliveries prefetched and waiting for a worker."));
        instruments[MessagingMetrics.ThrottledName]
            .Should().Be(("Counter`1", "{event}", "Broker throttling responses observed by the host."));
        instruments[MessagingMetrics.LockRenewalsName]
            .Should().Be(("Counter`1", "{renewal}", "Lock renewal attempts by outcome."));

        // The pre-existing tier stays where dashboards already expect it.
        instruments.Should().ContainKey(MessagingMetrics.ProcessDurationName);
    }

    [TestMethod]
    public void AdvancedInstrumentsLiveOnTheirOwnMeter()
    {
        MessagingMetrics.AdvancedMeterName.Should().Be("Ark.MediatorFramework.Messaging.Advanced");

        var instruments = _publishedInstruments(MessagingMetrics.AdvancedMeterName);

        instruments[MessagingMetrics.ReceiveBatchSizeName]
            .Should().Be(("Histogram`1", "{message}", "Deliveries returned by one receive call."));
        instruments[MessagingMetrics.ReceiveEmptyName]
            .Should().Be(("Counter`1", "{receive}", "Receive calls that returned no delivery."));
        instruments[MessagingMetrics.ReceiveBackoffIntervalName]
            .Should().Be(("Histogram`1", "s", "Idle wait applied between empty receives."));
        instruments[MessagingMetrics.ProcessQueueWaitName]
            .Should().Be(("Histogram`1", "s", "Time a delivery waits in the local buffer."));
        instruments[MessagingMetrics.ProcessSettleDurationName]
            .Should().Be(("Histogram`1", "s", "Time spent settling a delivery."));
        instruments[MessagingMetrics.ConcurrencyGradientName]
            .Should().Be(("Histogram`1", "1", "Latency gradient driving the concurrency brake."));
        instruments[MessagingMetrics.ConcurrencyDecisionName]
            .Should().Be(("Counter`1", "{decision}", "Concurrency limit changes by reason."));

        // Advanced names keep the plain prefix: the meter marks the tier, not the name.
        instruments.Keys.Should().AllSatisfy(static name => name.Should().StartWith("messaging."));
    }

    [TestMethod]
    public async Task AdvancedInstrumentsRecordNothingWhileGated()
    {
        const string queue = "metrics-gated";
        using var recorder = new Recorder(queue);

        await _runAsync(queue, advancedMetrics: false).ConfigureAwait(false);

        recorder._names(MessagingMetrics.AdvancedMeterName).Should().BeEmpty();
        recorder._names(OpenTelemetryProcessingMetricsStep.MeterName)
            .Should().Contain(MessagingMetrics.BufferedName)
            .And.Contain(MessagingMetrics.InFlightName);
    }

    [TestMethod]
    public async Task AdvancedInstrumentsRecordOnceEnabled()
    {
        const string queue = "metrics-advanced";
        using var recorder = new Recorder(queue);

        await _runAsync(queue, advancedMetrics: true).ConfigureAwait(false);

        recorder._names(MessagingMetrics.AdvancedMeterName)
            .Should().Contain(MessagingMetrics.ReceiveBatchSizeName)
            .And.Contain(MessagingMetrics.ProcessQueueWaitName);
    }

    [TestMethod]
    public void AdvancedExtensionEnablesTheOptionsFlag()
    {
        var builder = new RecordingMeterProviderBuilder();
        try
        {
            new MessagingProcessingOptions().AdvancedMetrics.Should().BeFalse();

            builder.AddArkMessagingInstrumentation();
            builder._meters.Should().Equal(OpenTelemetryProcessingMetricsStep.MeterName);
            new MessagingProcessingOptions().AdvancedMetrics
                .Should().BeFalse("the operational tier alone must not turn the advanced tier on");

            builder.AddArkMessagingAdvancedInstrumentation();

            builder._meters.Should().Contain(MessagingMetrics.AdvancedMeterName);
            new MessagingProcessingOptions().AdvancedMetrics
                .Should().BeTrue("a registered advanced meter must never be silently empty");
        }
        finally
        {
            MessagingMetrics._disableAdvancedMetrics();
        }
    }

    [TestMethod]
    public async Task MetricAttributesStayWithinTheBoundedTopologyBudget()
    {
        const string queue = "metrics-attributes";
        using var recorder = new Recorder(queue);

        await _runAsync(queue, advancedMetrics: true, participant: "sample-participant").ConfigureAwait(false);

        var measurements = recorder._all();
        measurements.Should().NotBeEmpty();
        foreach (var measurement in measurements)
        {
            measurement.Tags.Select(static tag => tag.Key).Should().BeSubsetOf(_allowedTagKeys);
            measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("messaging.system", "ark.mediatorframework"));
            measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("ark.participant", "sample-participant"));
        }
    }

    [TestMethod]
    public async Task OperationalTierAloneSupportsAScaleOutDecision()
    {
        const string queue = "metrics-scaleout";
        using var recorder = new Recorder(queue);

        await _runAsync(queue, advancedMetrics: false, concurrency: 4).ConfigureAwait(false);

        // Limit, buffered and in-flight are deltas: a stopped host leaves them all back at zero, and
        // the peak of buffered plus in-flight against the limit is the scale-out input.
        recorder._sum(MessagingMetrics.ConcurrencyLimitName).Should().Be(0);
        recorder._sum(MessagingMetrics.InFlightName).Should().Be(0);
        recorder._sum(MessagingMetrics.BufferedName).Should().Be(0);
        recorder._peak(MessagingMetrics.ConcurrencyLimitName).Should().Be(4);
        recorder._peak(MessagingMetrics.BufferedName).Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task AThrowingListenerCannotChangeMessagingBehaviour()
    {
        const string queue = "metrics-throwing";
        using var listener = new MeterListener();
        listener.InstrumentPublished = static (instrument, subscriber) =>
        {
            if (instrument.Meter.Name.StartsWith(OpenTelemetryProcessingMetricsStep.MeterName, StringComparison.Ordinal))
                subscriber.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>(static (_, _, _, _) => throw new InvalidOperationException("listener"));
        listener.SetMeasurementEventCallback<long>(static (_, _, _, _) => throw new InvalidOperationException("listener"));
        listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => throw new InvalidOperationException("listener"));
        listener.Start();

        var completed = await _runAsync(queue, advancedMetrics: true).ConfigureAwait(false);

        completed.Should().Be(16, "instrumentation failures must never change settlement");
    }

    private static async Task<int> _runAsync(
        string queue,
        bool advancedMetrics,
        int concurrency = 2,
        string? participant = null)
    {
        var source = new FiniteSource(total: 16, maximumBatchSize: 4);
        var options = new MessagingProcessingOptions
        {
            InitialConcurrency = concurrency,
            MaximumConcurrency = concurrency,
            AdaptiveConcurrency = false,
            AdvancedMetrics = advancedMetrics,
            ShutdownTimeout = TimeSpan.FromSeconds(10)
        };

        var host = new MessagingProcessorHost(
            source,
            queue,
            static async (delivery, ctk) => await delivery.CompleteAsync(ctk).ConfigureAwait(false),
            options,
            concurrencyController: null,
            participant: participant);

        await using (host.ConfigureAwait(false))
        {
            await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (source._completed < 16)
            {
                timeout.Token.ThrowIfCancellationRequested();
                await Task.Delay(5, timeout.Token).ConfigureAwait(false);
            }

            await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return source._completed;
    }

    private static Dictionary<string, (string Kind, string? Unit, string? Description)> _publishedInstruments(string meter)
    {
        RuntimeHelpers.RunClassConstructor(typeof(MessagingMetrics).TypeHandle);

        var found = new Dictionary<string, (string, string?, string?)>(StringComparer.Ordinal);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (string.Equals(instrument.Meter.Name, meter, StringComparison.Ordinal))
                found[instrument.Name] = (instrument.GetType().Name, instrument.Unit, instrument.Description);
        };
        listener.Start();
        return found;
    }

    /// <summary>Captures every measurement of one queue across both tiers.</summary>
    private sealed class Recorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<(string Meter, string Name, double Value, KeyValuePair<string, object?>[] Tags)> _measurements = new();

        internal Recorder(string queue)
        {
            _listener.InstrumentPublished = static (instrument, subscriber) =>
            {
                if (instrument.Meter.Name.StartsWith(OpenTelemetryProcessingMetricsStep.MeterName, StringComparison.Ordinal))
                    subscriber.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => _capture(queue, instrument, value, tags));
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => _capture(queue, instrument, value, tags));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => _capture(queue, instrument, value, tags));
            _listener.Start();
        }

        internal IReadOnlyList<(string Meter, string Name, double Value, KeyValuePair<string, object?>[] Tags)> _all()
        {
            return [.. _measurements];
        }

        internal IReadOnlyList<string> _names(string meter)
        {
            return [.. _measurements.Where(m => string.Equals(m.Meter, meter, StringComparison.Ordinal)).Select(static m => m.Name)];
        }

        internal double _sum(string name)
        {
            return _measurements.Where(m => string.Equals(m.Name, name, StringComparison.Ordinal)).Sum(static m => m.Value);
        }

        internal double _peak(string name)
        {
            var peak = 0d;
            var running = 0d;
            foreach (var measurement in _measurements.Where(m => string.Equals(m.Name, name, StringComparison.Ordinal)))
            {
                running += measurement.Value;
                peak = Math.Max(peak, running);
            }

            return peak;
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        private void _capture(string queue, Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var copy = tags.ToArray();
            // Other tests share the process and the meters, so only this queue's measurements count.
            if (!copy.Any(tag => string.Equals(tag.Key, "messaging.destination.name", StringComparison.Ordinal)
                    && string.Equals(tag.Value as string, queue, StringComparison.Ordinal)))
            {
                return;
            }

            _measurements.Enqueue((instrument.Meter.Name, instrument.Name, value, copy));
        }
    }

    /// <summary>A meter provider builder that only records what an extension registered.</summary>
    private sealed class RecordingMeterProviderBuilder : MeterProviderBuilder
    {
        internal List<string> _meters { get; } = [];

        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
        {
            return this;
        }

        public override MeterProviderBuilder AddMeter(params string[] names)
        {
            _meters.AddRange(names);
            return this;
        }
    }

    /// <summary>A source that hands out a fixed number of deliveries and then goes idle.</summary>
    private sealed class FiniteSource : IMessagingMessageSource
    {
        private readonly int _total;
        private int _issued;
        private int _settled;

        internal FiniteSource(int total, int maximumBatchSize)
        {
            _total = total;
            ReceiverCapabilities = new MessagingReceiverCapabilities(maximumBatchSize, true, true, TimeSpan.FromMinutes(1));
        }

        public MessagingReceiverCapabilities ReceiverCapabilities { get; }

        internal int _completed => Volatile.Read(ref _settled);

        public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
            string queue,
            int maxMessages,
            TimeSpan maxWait,
            CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            var count = Math.Min(maxMessages, Math.Max(0, _total - Volatile.Read(ref _issued)));
            if (count == 0)
            {
                await Task.Delay(maxWait, ctk).ConfigureAwait(false);
                return [];
            }

            Interlocked.Add(ref _issued, count);
            var batch = new IMessagingLockedDelivery[count];
            for (var index = 0; index < count; index++)
                batch[index] = new FiniteDelivery(this);

            return batch;
        }

        private sealed class FiniteDelivery : IMessagingLockedDelivery
        {
            private readonly FiniteSource _source;

            internal FiniteDelivery(FiniteSource source)
            {
                _source = source;
            }

            public IReadOnlyDictionary<string, string> Headers { get; } =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public ReadOnlySequence<byte> Payload => ReadOnlySequence<byte>.Empty;

            public int DeliveryCount => 1;

            public string DeliveryId { get; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            public DateTimeOffset? LockedUntil => DateTimeOffset.UtcNow.AddMinutes(1);

            public Task CompleteAsync(CancellationToken ctk)
            {
                Interlocked.Increment(ref _source._settled);
                return Task.CompletedTask;
            }

            public Task AbandonAsync(CancellationToken ctk)
            {
                return Task.CompletedTask;
            }

            public Task DeadLetterAsync(string reason, string description, CancellationToken ctk)
            {
                return Task.CompletedTask;
            }

            public Task RenewLockAsync(CancellationToken ctk)
            {
                return Task.CompletedTask;
            }
        }
    }
}
