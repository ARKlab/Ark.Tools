// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the host-scoped lock renewer: cadence, buffer coverage, failure and settlement.</summary>
[TestClass]
public sealed class MessagingLockRenewerTests
{
    private static readonly Instant _start = Instant.FromUtc(2026, 1, 1, 0, 0);

    [TestMethod]
    public async Task RenewalFiresAtHalfOfTheRemainingLock()
    {
        var clock = new FakeClock(_start);
        await using var renewer = new MessagingLockRenewer(_options(), clock);
        var delivery = new FakeDelivery(clock, TimeSpan.FromMinutes(1));
        var tracked = renewer._register(delivery);

        // Half of a one-minute lock has not elapsed yet.
        clock.AdvanceSeconds(20);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(0);

        // Past the halfway point the renewal is due.
        clock.AdvanceSeconds(15);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(1);

        // The refreshed lock restarts the cadence rather than renewing every tick.
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(1);

        clock.AdvanceSeconds(35);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(2);

        await tracked.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task TheSafetyMarginWinsForShortLocks()
    {
        var clock = new FakeClock(_start);
        var options = _options();
        options.RenewalSafetyMargin = TimeSpan.FromSeconds(10);
        await using var renewer = new MessagingLockRenewer(options, clock);
        var delivery = new FakeDelivery(clock, TimeSpan.FromSeconds(12));

        renewer._register(delivery);

        // Half of twelve seconds is six, but the margin keeps ten seconds of headroom.
        clock.AdvanceSeconds(1);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(0);

        clock.AdvanceSeconds(2);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(1);
    }

    [TestMethod]
    public async Task ADeliveryWaitingInTheBufferIsStillRenewed()
    {
        var clock = new FakeClock(_start);
        await using var renewer = new MessagingLockRenewer(_options(), clock);
        var buffered = new FakeDelivery(clock, TimeSpan.FromMinutes(1));
        var inFlight = new FakeDelivery(clock, TimeSpan.FromMinutes(1));

        renewer._register(buffered);
        var started = renewer._register(inFlight);

        clock.AdvanceSeconds(35);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);

        buffered._renewals.Should().Be(1, "registration happens at buffer entry, not when a worker starts");
        inFlight._renewals.Should().Be(1);
        renewer._count.Should().Be(2);

        await started.CompleteAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task OneTimerServesAnyNumberOfDeliveries()
    {
        var clock = new FakeClock(_start);
        var options = _options();
        options.MaximumRenewalBatch = 4;
        await using var renewer = new MessagingLockRenewer(options, clock);
        var deliveries = Enumerable
            .Range(0, 10)
            .Select(_ => new FakeDelivery(clock, TimeSpan.FromMinutes(1)))
            .ToArray();
        foreach (var delivery in deliveries)
            renewer._register(delivery);

        clock.AdvanceSeconds(35);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);

        deliveries.Count(static delivery => delivery._renewals == 1)
            .Should().Be(4, "a tick renews at most MaximumRenewalBatch locks so a big in-flight set cannot stall it");

        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        deliveries.Should().AllSatisfy(static delivery => delivery._renewals.Should().Be(1), "later ticks catch up");
    }

    [TestMethod]
    public async Task RenewalFailureCancelsTheHandlerAndDropsTheRegistration()
    {
        var clock = new FakeClock(_start);
        await using var renewer = new MessagingLockRenewer(_options(), clock);
        var delivery = new FakeDelivery(clock, TimeSpan.FromMinutes(1)) { _failRenewal = true };
        var tracked = (IMessagingRenewedDelivery)renewer._register(delivery);

        tracked._lockLost.IsCancellationRequested.Should().BeFalse();

        clock.AdvanceSeconds(35);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);

        tracked._lockLost.IsCancellationRequested.Should().BeTrue("a lost lock must stop the handler");
        renewer._count.Should().Be(0, "a delivery whose lock is gone is no longer renewable");
    }

    [TestMethod]
    public async Task SettlementDeregistersAndStopsFurtherRenewal()
    {
        var clock = new FakeClock(_start);
        await using var renewer = new MessagingLockRenewer(_options(), clock);
        var delivery = new FakeDelivery(clock, TimeSpan.FromMinutes(1));
        var tracked = renewer._register(delivery);

        clock.AdvanceSeconds(35);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        await tracked.CompleteAsync(CancellationToken.None).ConfigureAwait(false);

        renewer._count.Should().Be(0);
        delivery._completed.Should().Be(1, "settlement still reaches the transport with the refreshed lock");

        clock.AdvanceSeconds(60);
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(1, "a settled delivery is never renewed again");
    }

    [TestMethod]
    public async Task RenewalNeverRunsConcurrentlyWithSettlementOfTheSameDelivery()
    {
        var clock = new FakeClock(_start);
        await using var renewer = new MessagingLockRenewer(_options(), clock);
        var delivery = new FakeDelivery(clock, TimeSpan.FromMinutes(1)) { _blockSettle = true };
        var tracked = renewer._register(delivery);

        clock.AdvanceSeconds(35);
        var settlement = tracked.CompleteAsync(CancellationToken.None);
        await delivery._settleEntered.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // A tick that lands while the settle call is in flight must not renew behind its back.
        await renewer._tickAsync(CancellationToken.None).ConfigureAwait(false);
        delivery._renewals.Should().Be(0);
        delivery._maximumConcurrentOperations.Should().Be(1);

        delivery._settleRelease.SetResult();
        await settlement.ConfigureAwait(false);
        delivery._maximumConcurrentOperations.Should().Be(1, "renewal and settlement are serialised per delivery");
    }

    [TestMethod]
    public void NonRenewableTransportsFailCompositionWhenTheDurationsCannotFit()
    {
        var source = new NonRenewableSource(TimeSpan.FromSeconds(30));
        var options = new MessagingProcessingOptions
        {
            InitialConcurrency = 1,
            MaximumConcurrency = 4,
            ExpectedHandlerDuration = TimeSpan.FromSeconds(10)
        };

        var act = () => new MessagingProcessorHost(
            source,
            "queue",
            static (_, _) => Task.CompletedTask,
            options,
            TimeSpan.FromMinutes(5));

        act.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ProcessingOptionsInvalid);
    }

    [TestMethod]
    public async Task NonRenewableTransportsComposeWhenTheDurationsFit()
    {
        var source = new NonRenewableSource(TimeSpan.FromMinutes(10));
        var options = new MessagingProcessingOptions
        {
            InitialConcurrency = 1,
            MaximumConcurrency = 4,
            ExpectedHandlerDuration = TimeSpan.FromSeconds(1)
        };

        await using var host = new MessagingProcessorHost(
            source,
            "queue",
            static (_, _) => Task.CompletedTask,
            options,
            TimeSpan.FromMinutes(1));

        host.PrefetchBudget.Should().BeGreaterThan(0);
    }

    private static MessagingProcessingOptions _options()
    {
        return new MessagingProcessingOptions
        {
            RenewalSafetyMargin = TimeSpan.FromSeconds(5),
            RenewalScanInterval = TimeSpan.FromMilliseconds(10),
            InitialConcurrency = 1,
            MaximumConcurrency = 4
        };
    }

    private sealed class NonRenewableSource : IMessagingMessageSource
    {
        internal NonRenewableSource(TimeSpan lockDuration)
        {
            ReceiverCapabilities = new MessagingReceiverCapabilities(1, false, false, lockDuration);
        }

        public MessagingReceiverCapabilities ReceiverCapabilities { get; }

        public ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
            string queue,
            int maxMessages,
            TimeSpan maxWait,
            CancellationToken ctk)
        {
            return ValueTask.FromResult<IReadOnlyList<IMessagingLockedDelivery>>([]);
        }
    }

    /// <summary>A delivery whose lock follows a fake clock and that records operation overlap.</summary>
    private sealed class FakeDelivery : IMessagingLockedDelivery
    {
        private readonly FakeClock _clock;
        private readonly TimeSpan _lockDuration;
        private int _concurrent;

        internal FakeDelivery(FakeClock clock, TimeSpan lockDuration)
        {
            _clock = clock;
            _lockDuration = lockDuration;
            LockedUntil = clock.GetCurrentInstant().ToDateTimeOffset() + lockDuration;
        }

        internal bool _failRenewal { get; init; }

        internal bool _blockSettle { get; init; }

        internal int _renewals { get; private set; }

        internal int _completed { get; private set; }

        internal int _maximumConcurrentOperations { get; private set; }

        internal TaskCompletionSource _settleEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource _settleRelease { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyDictionary<string, string> Headers { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public ReadOnlySequence<byte> Payload => ReadOnlySequence<byte>.Empty;

        public int DeliveryCount => 1;

        public string DeliveryId => "fake";

        public DateTimeOffset? LockedUntil { get; private set; }

        public Task RenewLockAsync(CancellationToken ctk)
        {
            _enter();
            try
            {
                if (_failRenewal)
                    throw new InvalidOperationException("The lock is gone.");

                _renewals++;
                LockedUntil = _clock.GetCurrentInstant().ToDateTimeOffset() + _lockDuration;
                return Task.CompletedTask;
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            _enter();
            try
            {
                _settleEntered.TrySetResult();
                if (_blockSettle)
                    await _settleRelease.Task.ConfigureAwait(false);

                _completed++;
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }

        public Task AbandonAsync(CancellationToken ctk)
        {
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(string reason, string description, CancellationToken ctk)
        {
            return Task.CompletedTask;
        }

        private void _enter()
        {
            var current = Interlocked.Increment(ref _concurrent);
            if (current > _maximumConcurrentOperations)
                _maximumConcurrentOperations = current;
        }
    }
}
