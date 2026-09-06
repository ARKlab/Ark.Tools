// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using System.Buffers;
using System.Collections.Concurrent;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the bounded buffer, credit accounting and drain of the processor host.</summary>
[TestClass]
public sealed class MessagingProcessorHostTests
{
    [TestMethod]
    public void PrefetchBudgetDerivesFromConcurrencyAndLockDuration()
    {
        var options = new MessagingProcessingOptions
        {
            InitialConcurrency = 4,
            MaximumConcurrency = 64,
            PrefetchMultiplier = 2,
            LockSafetyFactor = 0.5,
            ExpectedHandlerDuration = TimeSpan.FromSeconds(1)
        };

        // A renewing transport is not clamped by the lock duration.
        options.ComputePrefetchBudget(4, new MessagingReceiverCapabilities(32, true, true, TimeSpan.FromSeconds(30)))
            .Should().Be(8);

        // Without renewal the buffer must drain inside half the lock: 4 * (5s / 1s) = 20, so the multiplier wins.
        options.ComputePrefetchBudget(4, new MessagingReceiverCapabilities(32, false, false, TimeSpan.FromSeconds(10)))
            .Should().Be(8);

        // A short lock clamps below the multiplier: 4 * (0.5s / 1s) = 2.
        options.ComputePrefetchBudget(4, new MessagingReceiverCapabilities(32, false, false, TimeSpan.FromSeconds(1)))
            .Should().Be(2);

        // The hard ceiling wins over the multiplier.
        options.MaximumPrefetch = 5;
        options.ComputePrefetchBudget(4, new MessagingReceiverCapabilities(32, true, true, null))
            .Should().Be(5);
    }

    [TestMethod]
    public void MaximumPrefetchBelowMaximumConcurrencyIsRejected()
    {
        var options = new MessagingProcessingOptions
        {
            MaximumConcurrency = 8,
            InitialConcurrency = 1,
            MaximumPrefetch = 4
        };

        var act = options.Validate;

        act.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ProcessingOptionsInvalid);
    }

    [TestMethod]
    public async Task CreditInvariantHoldsUnderContinuousBacklog()
    {
        var source = new ScriptedSource(maximumBatchSize: 4);
        var inFlight = 0;
        var peakInFlight = 0;
        await using var host = new MessagingProcessorHost(
            source,
            "queue",
            async (delivery, ctk) =>
            {
                _interlockedMax(ref peakInFlight, Interlocked.Increment(ref inFlight));
                await Task.Delay(5, ctk).ConfigureAwait(false);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
                Interlocked.Decrement(ref inFlight);
            },
            new MessagingProcessingOptions { InitialConcurrency = 4, MaximumConcurrency = 8 });

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        var peakOutstanding = 0;
        while (source._delivered < 50)
        {
            _interlockedMax(ref peakOutstanding, host.Outstanding);
            await Task.Delay(1).ConfigureAwait(false);
        }

        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        source._maximumRequested.Should().BeLessThanOrEqualTo(4, "a receive never asks for more than the batch maximum");
        peakOutstanding.Should().BeLessThanOrEqualTo(host.PrefetchBudget, "buffered plus in-flight plus requested stays within the budget");
        peakInFlight.Should().BeLessThanOrEqualTo(host.Concurrency, "only the workers dispatch");
    }

    [TestMethod]
    public async Task FullBufferBlocksTheReceiveLoop()
    {
        var source = new ScriptedSource(maximumBatchSize: 4);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new MessagingProcessorHost(
            source,
            "queue",
            async (delivery, ctk) =>
            {
                await release.Task.WaitAsync(ctk).ConfigureAwait(false);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
            },
            new MessagingProcessingOptions { InitialConcurrency = 2, MaximumConcurrency = 8 });

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => host.Outstanding == host.PrefetchBudget).ConfigureAwait(false);
        var deliveredWhenFull = source._delivered;
        var receivesWhenFull = source._receives;
        await Task.Delay(200).ConfigureAwait(false);

        source._delivered.Should().Be(deliveredWhenFull, "a full budget must stop the receive loop entirely");
        source._receives.Should().Be(receivesWhenFull, "no extra receive call is made while the buffer is full");

        release.SetResult();
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task WorkersDispatchConcurrentlyRatherThanSerialising()
    {
        var source = new ScriptedSource(maximumBatchSize: 4);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var concurrent = 0;
        var peak = 0;
        await using var host = new MessagingProcessorHost(
            source,
            "queue",
            async (delivery, ctk) =>
            {
                _interlockedMax(ref peak, Interlocked.Increment(ref concurrent));
                await gate.Task.WaitAsync(ctk).ConfigureAwait(false);
                Interlocked.Decrement(ref concurrent);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
            },
            new MessagingProcessingOptions { InitialConcurrency = 4, MaximumConcurrency = 8 });

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => Volatile.Read(ref peak) >= 4).ConfigureAwait(false);
        gate.SetResult();
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        peak.Should().Be(4, "the four workers overlap and nothing dispatches beyond them");
    }

    [TestMethod]
    public async Task CreditIsReleasedOnlyAfterSettlement()
    {
        var source = new ScriptedSource(maximumBatchSize: 1, total: 1);
        var settling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = new MessagingProcessorHost(
            source,
            "queue",
            async (delivery, ctk) =>
            {
                await settling.Task.WaitAsync(ctk).ConfigureAwait(false);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
            },
            new MessagingProcessingOptions { InitialConcurrency = 1, MaximumConcurrency = 1 });

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => host.Outstanding >= 1).ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);

        source._settlements.Should().BeEmpty("the handler has not settled yet");
        var outstandingBeforeSettlement = host.Outstanding;
        outstandingBeforeSettlement.Should().BeGreaterThan(0, "credit is held for an unsettled delivery");

        settling.SetResult();
        await _waitUntilAsync(() => !source._settlements.IsEmpty).ConfigureAwait(false);
        await _waitUntilAsync(() => host.Outstanding < outstandingBeforeSettlement).ConfigureAwait(false);

        source._settlements.Should().ContainSingle().Which.Should().Be("complete");
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ShutdownDrainsBufferedWorkWithinTheWindow()
    {
        var source = new ScriptedSource(maximumBatchSize: 4, total: 8);
        var completed = 0;
        var host = new MessagingProcessorHost(
            source,
            "queue",
            async (delivery, ctk) =>
            {
                await Task.Delay(50, ctk).ConfigureAwait(false);
                await delivery.CompleteAsync(ctk).ConfigureAwait(false);
                Interlocked.Increment(ref completed);
            },
            new MessagingProcessingOptions
            {
                InitialConcurrency = 2,
                MaximumConcurrency = 8,
                ShutdownTimeout = TimeSpan.FromSeconds(10)
            });

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => host.Outstanding == host.PrefetchBudget).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        completed.Should().BeGreaterThanOrEqualTo(host.PrefetchBudget, "in-flight and buffered work finishes inside the window");
        host.AbandonedOnShutdown.Should().Be(0);
        source._abandoned.Should().Be(0);
        await host.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ShutdownAbandonsTheRemainderWhenTheWindowElapses()
    {
        var source = new ScriptedSource(maximumBatchSize: 1);
        var host = new MessagingProcessorHost(
            source,
            "queue",
            static async (delivery, ctk) => await Task.Delay(Timeout.Infinite, ctk).ConfigureAwait(false),
            new MessagingProcessingOptions
            {
                InitialConcurrency = 1,
                MaximumConcurrency = 1,
                ShutdownTimeout = TimeSpan.FromMilliseconds(200)
            });

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => host.Outstanding == host.PrefetchBudget).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        source._abandoned.Should().Be(host.PrefetchBudget, "nothing may still hold a lock once the drain window elapsed");
        await host.DisposeAsync().ConfigureAwait(false);
    }

    private static void _interlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    private static async Task _waitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!condition())
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Delay(5, timeout.Token).ConfigureAwait(false);
        }
    }

    /// <summary>A backlogged source that records the credit the host asked for and every settlement.</summary>
    private sealed class ScriptedSource : IMessagingMessageSource
    {
        private readonly int? _total;
        private int _issued;
        private int _receiveCount;

        internal ScriptedSource(int maximumBatchSize, int? total = null)
        {
            _total = total;
            ReceiverCapabilities = new MessagingReceiverCapabilities(maximumBatchSize, true, true, null);
        }

        public MessagingReceiverCapabilities ReceiverCapabilities { get; }

        internal ConcurrentQueue<string> _settlements { get; } = new();

        internal int _delivered => Volatile.Read(ref _issued);

        internal int _receives => Volatile.Read(ref _receiveCount);

        internal int _abandoned => _settlements.Count(static settlement => settlement == "abandon");

        internal int _maximumRequested { get; private set; }

        public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
            string queue,
            int maxMessages,
            TimeSpan maxWait,
            CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _receiveCount);
            _maximumRequested = Math.Max(_maximumRequested, maxMessages);

            var remaining = _total is { } total ? Math.Max(0, total - Volatile.Read(ref _issued)) : maxMessages;
            var count = Math.Min(maxMessages, remaining);
            if (count == 0)
            {
                // A server-side-wait source holds the call open instead of returning immediately.
                await Task.Delay(maxWait, ctk).ConfigureAwait(false);
                return [];
            }

            var batch = new IMessagingLockedDelivery[count];
            for (var index = 0; index < count; index++)
                batch[index] = new ScriptedDelivery(_settlements);

            Interlocked.Add(ref _issued, count);
            return batch;
        }

        private sealed class ScriptedDelivery : IMessagingLockedDelivery
        {
            private readonly ConcurrentQueue<string> _settlements;

            internal ScriptedDelivery(ConcurrentQueue<string> settlements)
            {
                _settlements = settlements;
            }

            public IReadOnlyDictionary<string, string> Headers { get; } =
                new Dictionary<string, string>(StringComparer.Ordinal);

            public ReadOnlySequence<byte> Payload => ReadOnlySequence<byte>.Empty;

            public int DeliveryCount => 1;

            public string DeliveryId { get; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

            public DateTimeOffset? LockedUntil => null;

            public Task CompleteAsync(CancellationToken ctk)
            {
                _settlements.Enqueue("complete");
                return Task.CompletedTask;
            }

            public Task AbandonAsync(CancellationToken ctk)
            {
                _settlements.Enqueue("abandon");
                return Task.CompletedTask;
            }

            public Task DeadLetterAsync(string reason, string description, CancellationToken ctk)
            {
                _settlements.Enqueue("deadletter");
                return Task.CompletedTask;
            }
        }
    }
}
