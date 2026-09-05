// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using System.Buffers;
using System.Collections.Concurrent;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the independent empty, error and no-credit waits of a receive loop.</summary>
[TestClass]
public sealed class MessagingReceiveBackoffTests
{
    [TestMethod]
    public void EmptyResultsDoubleTheCapUpToTheMaximum()
    {
        var backoff = new MessagingReceiveBackoff(_options(), static () => 1);

        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(50));
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(100));
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(200));
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(400));
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(800));
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(1600));
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(3200));
        backoff._onEmpty().Should().Be(TimeSpan.FromSeconds(5));
        backoff._onEmpty().Should().Be(TimeSpan.FromSeconds(5), "the cap never grows past the maximum");
    }

    [TestMethod]
    public void ANonEmptyBatchResetsTheCapToTheMinimum()
    {
        var backoff = new MessagingReceiveBackoff(_options(), static () => 1);
        backoff._onEmpty();
        backoff._onEmpty();
        backoff._onEmpty();

        backoff._onReceived();

        backoff._consecutiveEmptyResults.Should().Be(0);
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(50));
    }

    [TestMethod]
    public void FullJitterStaysWithinTheCapAndVaries()
    {
        var samples = new Queue<double>([0, 0.25, 0.5, 0.99]);
        var backoff = new MessagingReceiveBackoff(_options(), samples.Dequeue);
        var cap = TimeSpan.FromSeconds(1);

        var drawn = new[] { backoff._sample(cap), backoff._sample(cap), backoff._sample(cap), backoff._sample(cap) };

        drawn.Should().AllSatisfy(delay =>
        {
            delay.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            delay.Should().BeLessThanOrEqualTo(cap);
        });
        drawn.Distinct().Should().HaveCount(4, "a constant wait would synchronise replicas");
    }

    [TestMethod]
    public void TheErrorCooldownIsIndependentOfTheEmptyCounter()
    {
        var backoff = new MessagingReceiveBackoff(_options(), static () => 1);
        backoff._onEmpty();
        backoff._onEmpty();

        var cooldown = backoff._sampleErrorCooldown();

        cooldown.Should().Be(TimeSpan.FromSeconds(10));
        backoff._consecutiveEmptyResults.Should().Be(2, "an error must not disturb the empty backoff");
        backoff._onEmpty().Should().Be(TimeSpan.FromMilliseconds(200), "the empty backoff continues where it was");
    }

    [TestMethod]
    public void TheErrorCooldownIsJitteredWithinItsUpperHalf()
    {
        var lowest = new MessagingReceiveBackoff(_options(), static () => 0)._sampleErrorCooldown();
        var highest = new MessagingReceiveBackoff(_options(), static () => 1)._sampleErrorCooldown();

        lowest.Should().Be(TimeSpan.FromSeconds(5));
        highest.Should().Be(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public void MinPollIntervalAboveMaxPollIntervalIsRejected()
    {
        var options = new MessagingProcessingOptions
        {
            MinPollInterval = TimeSpan.FromSeconds(10),
            MaxPollInterval = TimeSpan.FromSeconds(5),
            InitialConcurrency = 1
        };

        var act = options.Validate;

        act.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ProcessingOptionsInvalid);
    }

    [TestMethod]
    public async Task ServerSideWaitTransportsGrowTheWaitWindowInsteadOfSleeping()
    {
        var source = new RecordingSource(serverSideWait: true);
        var options = _options();
        options.InitialConcurrency = 1;
        options.MaximumConcurrency = 1;
        options.ReceiveWaitTime = TimeSpan.FromMilliseconds(10);
        options.MaxPollInterval = TimeSpan.FromMilliseconds(80);
        await using var host = new MessagingProcessorHost(source, "queue", _neverCalled, options, null, null, static () => 1, null);

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => source._waits.Count >= 5).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        var waits = source._waits.Take(5).ToArray();
        waits[0].Should().Be(TimeSpan.FromMilliseconds(10), "the first call uses the configured wait");
        waits.Should().BeInAscendingOrder("the window grows while the queue stays empty");
        waits.Should().AllSatisfy(static wait => wait.Should().BeLessThanOrEqualTo(TimeSpan.FromMilliseconds(80)));
        waits[4].Should().Be(TimeSpan.FromMilliseconds(80), "growth stops at the maximum poll interval");
    }

    [TestMethod]
    public async Task PollingTransportsKeepTheConfiguredWaitWindow()
    {
        var source = new RecordingSource(serverSideWait: false);
        var options = _options();
        options.InitialConcurrency = 1;
        options.MaximumConcurrency = 1;
        options.ReceiveWaitTime = TimeSpan.FromMilliseconds(10);
        options.MinPollInterval = TimeSpan.FromMilliseconds(1);
        options.MaxPollInterval = TimeSpan.FromMilliseconds(20);
        await using var host = new MessagingProcessorHost(source, "queue", _neverCalled, options, null, null, static () => 1, null);

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => source._waits.Count >= 3).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        source._waits.Should().AllSatisfy(
            static wait => wait.Should().Be(TimeSpan.FromMilliseconds(10)),
            "a transport without server-side wait is not asked to wait longer; the host sleeps instead");
    }

    [TestMethod]
    public async Task ATransportErrorCoolsDownAndTheLoopKeepsRunning()
    {
        var source = new RecordingSource(serverSideWait: true, failures: 1);
        var options = _options();
        options.InitialConcurrency = 1;
        options.MaximumConcurrency = 1;
        options.ReceiveWaitTime = TimeSpan.FromMilliseconds(10);
        options.ErrorCooldown = TimeSpan.FromMilliseconds(100);
        await using var host = new MessagingProcessorHost(source, "queue", _neverCalled, options, null, null, static () => 0, null);

        await host.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await _waitUntilAsync(() => source._waits.Count >= 2).ConfigureAwait(false);
        await host.StopAsync(CancellationToken.None).ConfigureAwait(false);

        source._failed.Should().Be(1);
        host.Outstanding.Should().Be(0, "the credit taken for the failed receive is released");
        source._elapsedBetweenFirstTwoCalls.Should().BeGreaterThanOrEqualTo(
            TimeSpan.FromMilliseconds(45),
            "a failed receive waits the jittered cooldown before retrying");
    }

    private static Task _neverCalled(IMessagingLockedDelivery delivery, CancellationToken ctk)
    {
        throw new InvalidOperationException("The scripted source never yields a delivery.");
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

    /// <summary>An always-empty source that records the wait window it was asked for.</summary>
    private sealed class RecordingSource : IMessagingMessageSource
    {
        private readonly int _failures;
        private readonly List<long> _timestamps = [];
        private int _calls;

        internal RecordingSource(bool serverSideWait, int failures = 0)
        {
            _failures = failures;
            ReceiverCapabilities = new MessagingReceiverCapabilities(1, serverSideWait, true, null);
        }

        public MessagingReceiverCapabilities ReceiverCapabilities { get; }

        internal ConcurrentQueue<TimeSpan> _waits { get; } = new();

        internal int _failed { get; private set; }

        internal TimeSpan _elapsedBetweenFirstTwoCalls
        {
            get
            {
                lock (_timestamps)
                {
                    return _timestamps.Count < 2
                        ? TimeSpan.Zero
                        : TimeSpan.FromTicks(_timestamps[1] - _timestamps[0]);
                }
            }
        }

        public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
            string queue,
            int maxMessages,
            TimeSpan maxWait,
            CancellationToken ctk)
        {
            ctk.ThrowIfCancellationRequested();
            lock (_timestamps)
                _timestamps.Add(DateTime.UtcNow.Ticks);

            _waits.Enqueue(maxWait);
            if (_calls++ < _failures)
            {
                _failed++;
                throw new TimeoutException("The scripted broker is unavailable.");
            }

            // A server-side-wait source holds the call open for the requested window.
            await Task.Delay(maxWait, ctk).ConfigureAwait(false);
            return [];
        }
    }

    private static MessagingProcessingOptions _options()
    {
        return new MessagingProcessingOptions
        {
            MinPollInterval = TimeSpan.FromMilliseconds(50),
            MaxPollInterval = TimeSpan.FromSeconds(5),
            ErrorCooldown = TimeSpan.FromSeconds(10)
        };
    }
}
