// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Hosting;

using NLog;

using NodaTime;

using System.Diagnostics;
using System.Threading.Channels;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Processes one participant queue with a bounded buffer and a pool of async workers.</summary>
/// <remarks>
/// The host owns the receive loop: it computes available credit, receives a batch no larger than the
/// transport's maximum, writes deliveries into a bounded channel, and lets workers dispatch them
/// concurrently. Credit is released only after settlement, so a slow settle cannot cause over-fetching.
/// </remarks>
public sealed class MessagingProcessorHost : IHostedService, IAsyncDisposable
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly IMessagingMessageSource _source;
    private readonly Func<double>? _jitter;
    private readonly Func<IMessagingLockedDelivery, CancellationToken, Task> _onDelivery;
    private readonly string _queue;
    private readonly MessagingProcessingOptions _options;
    private readonly Channel<BufferedDelivery> _buffer;
    private readonly SemaphoreSlim _credits;
    private readonly int _batchSize;
    private readonly MessagingLockRenewer? _renewer;
    private readonly IMessagingConcurrencyController _controller;
    private readonly KeyValuePair<string, object?>[] _tags;
    private readonly Lock _gate = new();
    private int _prefetchBudget;
    private int _creditDebt;
    private int _outstanding;
    private int _workerTarget;
    private int _activeWorkers;
    private int _shrinkStreak;
    private CancellationTokenSource? _receiving;
    private CancellationTokenSource? _processing;
    private List<Task>? _workers;
    private Task? _receiveLoop;
    private Task? _renewalLoop;
    private Task? _controlLoop;

    /// <summary>Creates a processor host for one queue.</summary>
    /// <param name="source">The pull message source.</param>
    /// <param name="queue">The participant queue to process.</param>
    /// <param name="onDelivery">The per-delivery callback, normally <c>MessagingDispatcher.OnDeliveryAsync</c>.</param>
    /// <param name="options">The processing options, or <see langword="null"/> for the defaults.</param>
    /// <param name="maximumHandlerDuration">The participant's maximum handler duration, validated against a non-renewable lock.</param>
    /// <param name="concurrencyController">The concurrency controller, or <see langword="null"/> for the default AIMD controller.</param>
    /// <param name="participant">The participant identity used as a metric attribute, when it differs from the queue.</param>
    public MessagingProcessorHost(
        IMessagingMessageSource source,
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery,
        MessagingProcessingOptions? options = null,
        TimeSpan? maximumHandlerDuration = null,
        IMessagingConcurrencyController? concurrencyController = null,
        string? participant = null)
        : this(source, queue, onDelivery, options, maximumHandlerDuration, concurrencyController, null, null, participant)
    {
    }

    internal MessagingProcessorHost(
        IMessagingMessageSource source,
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery,
        MessagingProcessingOptions? options,
        TimeSpan? maximumHandlerDuration,
        IMessagingConcurrencyController? concurrencyController,
        Func<double>? jitter,
        IClock? clock,
        string? participant = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentNullException.ThrowIfNull(onDelivery);

        _source = source;
        _queue = queue;
        _onDelivery = onDelivery;
        _options = options ?? new MessagingProcessingOptions();
        _jitter = jitter;
        _options.Validate();
        _tags = MessagingMetrics._topologyTags(queue, participant);

        _controller = concurrencyController ?? new MessagingAimdConcurrencyController(_options, clock);
        _workerTarget = _controller.Limit;
        _prefetchBudget = _options.ComputePrefetchBudget(_workerTarget, source.ReceiverCapabilities);
        // Only the source's declared maximum bounds a request: the current budget is enforced by
        // credits, so a limit grown by adaptive concurrency is not capped by the initial budget.
        _batchSize = source.ReceiverCapabilities.MaximumBatchSize;
        _credits = new SemaphoreSlim(_prefetchBudget);

        // The channel is sized for the hard ceiling because credits, not the channel, enforce the
        // current budget: a bounded channel cannot be resized while the host runs.
        var ceiling = Math.Max(_prefetchBudget, _options.GetEffectiveMaximumPrefetch());
        _buffer = Channel.CreateBounded<BufferedDelivery>(new BoundedChannelOptions(ceiling)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        if (source.ReceiverCapabilities.SupportsLockRenewal)
        {
            _validateRenewableLock(source.ReceiverCapabilities);
            _renewer = new MessagingLockRenewer(_options, clock, _tags);
        }
        else
        {
            _validateNonRenewableLock(source.ReceiverCapabilities, maximumHandlerDuration);
        }
    }

    private void _validateRenewableLock(MessagingReceiverCapabilities capabilities)
    {
        if (capabilities.NativeLockDuration is not { } lockDuration)
            return;

        // Renewal only keeps a lock alive when the renewer gets a chance to run before it expires:
        // it scans on an interval and renews once the safety margin is reached, so a lock shorter
        // than both together expires no matter how the handler behaves.
        var minimum = _options.RenewalSafetyMargin + _options.RenewalScanInterval;
        if (lockDuration > minimum)
            return;

        throw new MessagingCompositionException(
            MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
            FormattableString.Invariant(
                $"Transport '{_queue}' declares a lock duration ({lockDuration}) that is not longer than RenewalSafetyMargin plus RenewalScanInterval ({minimum}); renewal could never run in time. Raise the lock duration (for Storage Queues, the receive visibility timeout) or lower the renewal margin."));
    }

    private void _validateNonRenewableLock(
        MessagingReceiverCapabilities capabilities,
        TimeSpan? maximumHandlerDuration)
    {
        if (capabilities.NativeLockDuration is not { } lockDuration || maximumHandlerDuration is not { } handler)
            return;

        // A delivery may sit in the buffer behind a full drain before a worker reaches it, and a
        // transport that cannot renew has no way to extend the lock while it waits.
        var concurrency = _workerTarget;
        var queued = Math.Max(0, _prefetchBudget - concurrency);
        var bufferWait = _options.ExpectedHandlerDuration * (queued / (double)concurrency);
        if (handler + bufferWait <= lockDuration)
            return;

        throw new MessagingCompositionException(
            MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
            FormattableString.Invariant(
                $"Transport '{_queue}' cannot renew locks and its lock duration ({lockDuration}) is shorter than MaximumHandlerDuration ({handler}) plus the expected buffer wait ({bufferWait}). Lower MaximumPrefetch or MaximumHandlerDuration, or raise the lock duration."));
    }

    /// <summary>Gets the number of workers dispatching deliveries.</summary>
    public int Concurrency => Volatile.Read(ref _workerTarget);

    /// <summary>Gets the maximum number of deliveries that may be buffered plus in flight.</summary>
    public int PrefetchBudget => Volatile.Read(ref _prefetchBudget);

    /// <summary>Gets the number of deliveries currently buffered or in flight.</summary>
    public int Outstanding => Volatile.Read(ref _outstanding);

    /// <summary>Gets the number of deliveries abandoned because the drain window elapsed.</summary>
    public int AbandonedOnShutdown { get; private set; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_receiving is not null)
                throw new InvalidOperationException("The messaging processor host has already been started.");

            var receiving = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var processing = new CancellationTokenSource();
            _receiving = receiving;
            _processing = processing;
            _workers = [];
            MessagingMetrics._recordConcurrencyLimit(_workerTarget, _tags);
            _startWorkers(_workerTarget, processing.Token);
            _receiveLoop = Task.Run(() => _receiveAsync(receiving.Token), CancellationToken.None);
            if (_renewer is not null)
            {
                var renewer = _renewer;
                _renewalLoop = Task.Run(() => renewer._runAsync(processing.Token), CancellationToken.None);
            }

            if (_options.AdaptiveConcurrency)
                _controlLoop = Task.Run(() => _controlAsync(processing.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? receiving;
        CancellationTokenSource? processing;
        List<Task>? workers;
        Task? receiveLoop;
        Task? renewalLoop;
        Task? controlLoop;
        lock (_gate)
        {
            receiving = _receiving;
            processing = _processing;
            workers = _workers;
            receiveLoop = _receiveLoop;
            renewalLoop = _renewalLoop;
            controlLoop = _controlLoop;
            _receiving = null;
            _processing = null;
            _workers = null;
            _receiveLoop = null;
            _renewalLoop = null;
            _controlLoop = null;
        }

        if (receiving is null || processing is null)
            return;

        using var receivingToDispose = receiving;
        using var processingToDispose = processing;

        // Stop receiving first so the buffer can only shrink from here.
        await receiving.CancelAsync().ConfigureAwait(false);
        await _observeAsync(receiveLoop).ConfigureAwait(false);
        _buffer.Writer.TryComplete();

        // Let in-flight and buffered work finish within the shutdown window.
        var drain = Task.WhenAll(workers ?? Enumerable.Empty<Task>());
        var drained = await _waitAsync(drain, _options.ShutdownTimeout, cancellationToken).ConfigureAwait(false);
        if (!drained)
        {
            await processing.CancelAsync().ConfigureAwait(false);
            await _observeAsync(drain).ConfigureAwait(false);
        }

        // The renewal timer and the controller have nothing left to keep alive once the workers are done.
        await processing.CancelAsync().ConfigureAwait(false);
        await _observeAsync(renewalLoop).ConfigureAwait(false);
        await _observeAsync(controlLoop).ConfigureAwait(false);

        // Abandon whatever never got processed so redelivery is immediate rather than
        // lock-expiry-delayed.
        while (_buffer.Reader.TryRead(out var buffered))
        {
            AbandonedOnShutdown++;
            MessagingMetrics._recordBuffered(-1, _tags);
            await _settleQuietlyAsync(buffered.Delivery).ConfigureAwait(false);
            _releaseCredit(1);
        }

        // The gauges are deltas, so a stopped host must return them to zero.
        MessagingMetrics._recordConcurrencyLimit(-Volatile.Read(ref _workerTarget), _tags);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (_renewer is not null)
            await _renewer.DisposeAsync().ConfigureAwait(false);

        _credits.Dispose();
    }

    private async Task _receiveAsync(CancellationToken ctk)
    {
        var backoff = new MessagingReceiveBackoff(_options, _jitter);
        var serverSideWait = _source.ReceiverCapabilities.SupportsServerSideWait;
        var waitWindow = _options.ReceiveWaitTime;
        while (!ctk.IsCancellationRequested)
        {
            // Acquiring credit before receiving is the backpressure: a full budget blocks here and
            // no receive call is made at all. No-credit therefore has no timer of its own.
            await _acquireCreditAsync(ctk).ConfigureAwait(false);
            var requested = 1;
            while (requested < _batchSize && _tryAcquireCredit())
                requested++;

            var received = 0;
            var failed = false;
            var advanced = _options.AdvancedMetrics;
            IReadOnlyList<IMessagingLockedDelivery> batch = [];
            try
            {
                batch = await _source
                    .ReceiveBatchAsync(_queue, requested, waitWindow, ctk)
                    .ConfigureAwait(false);
                foreach (var delivery in batch)
                {
                    // Renewal starts at buffer entry, not when a worker picks the delivery up: that
                    // is what keeps a prefetched lock alive while it waits.
                    var tracked = _renewer is null ? delivery : _renewer._register(delivery);

                    // Never cancel a write: the delivery is already locked, and dropping it here would
                    // hold that lock until it expires. Credit guarantees room in the buffer.
                    var buffered = new BufferedDelivery(tracked, advanced ? Stopwatch.GetTimestamp() : 0);
                    await _buffer.Writer.WriteAsync(buffered, CancellationToken.None).ConfigureAwait(false);

                    // Counted only once the delivery is a worker's problem: anything that throws before
                    // this point must give the credit back, or the budget shrinks by one for good and a
                    // host whose budget reaches zero never receives again.
                    received++;
                    MessagingMetrics._recordBuffered(1, _tags);
                }
            }
            catch (OperationCanceledException) when (ctk.IsCancellationRequested)
            {
                await _abandonUnbufferedAsync(batch, received).ConfigureAwait(false);
                throw;
            }
#pragma warning disable CA1031 // A broker failure must cool down, not kill the loop.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failed = true;
                if (exception is MessagingThrottledException)
                {
                    MessagingMetrics._recordThrottled(_tags);
                    _reportSignal(MessagingConcurrencySignal.BrokerThrottled);
                }

                // Whatever the broker already handed over but the host never buffered has to go back
                // now: nothing else in the process still knows about those locks.
                await _abandonUnbufferedAsync(batch, received).ConfigureAwait(false);
                var cooldown = backoff._sampleErrorCooldown();
                _logger.Warn(
                    exception,
                    CultureInfo.InvariantCulture,
                    "Receive failed on queue {queue}; retrying after {cooldown}.",
                    _queue,
                    cooldown);
                await _delayAsync(cooldown, ctk).ConfigureAwait(false);
            }
            finally
            {
                // Credit is only consumed by deliveries actually taken from the broker.
                if (requested > received)
                    _releaseCredit(requested - received);
            }

            // The error cooldown never touches the empty backoff, and vice versa.
            if (failed)
                continue;

            if (advanced)
                MessagingMetrics._recordReceiveBatch(received, _tags);

            if (received > 0)
            {
                backoff._onReceived();
                waitWindow = _options.ReceiveWaitTime;
                continue;
            }

            var cap = backoff._onEmpty();
            if (serverSideWait)
            {
                // The broker holds the call open, so growing the window lowers the request rate
                // without adding latency.
                waitWindow = cap > _options.ReceiveWaitTime ? cap : _options.ReceiveWaitTime;
                if (advanced)
                    MessagingMetrics._recordBackoffInterval(waitWindow, _tags);
            }
            else
            {
                var wait = backoff._sample(cap);
                if (advanced)
                    MessagingMetrics._recordBackoffInterval(wait, _tags);
                await _delayAsync(wait, ctk).ConfigureAwait(false);
            }
        }
    }

    private void _startWorkers(int count, CancellationToken ctk)
    {
        for (var index = 0; index < count; index++)
        {
            Interlocked.Increment(ref _activeWorkers);
            _workers!.Add(Task.Run(() => _workAsync(ctk), CancellationToken.None));
        }
    }

    /// <summary>Abandons the deliveries of a batch that never made it into the buffer.</summary>
    /// <param name="batch">The batch handed over by the transport.</param>
    /// <param name="buffered">The number of leading deliveries that reached the buffer.</param>
    /// <returns>A task that completes once the remainder is abandoned.</returns>
    private static async Task _abandonUnbufferedAsync(IReadOnlyList<IMessagingLockedDelivery> batch, int buffered)
    {
        for (var index = buffered; index < batch.Count; index++)
            await _settleQuietlyAsync(batch[index]).ConfigureAwait(false);
    }

    private bool _tryRetireWorker()
    {
        while (true)
        {
            var active = Volatile.Read(ref _activeWorkers);
            if (active <= Volatile.Read(ref _workerTarget))
                return false;
            if (Interlocked.CompareExchange(ref _activeWorkers, active - 1, active) == active)
                return true;
        }
    }

    private async Task _controlAsync(CancellationToken ctk)
    {
        while (!ctk.IsCancellationRequested)
        {
            await _delayAsync(_options.ConcurrencyEvaluationInterval, ctk).ConfigureAwait(false);
            if (ctk.IsCancellationRequested)
                return;

            if (await _probeThreadPoolAsync(ctk).ConfigureAwait(false) > _options.ThreadPoolStarvationThreshold)
                _reportSignal(MessagingConcurrencySignal.ThreadPoolStarved);

            // A drained queue makes measurements meaningless, so the controller is told whether work
            // was actually waiting rather than inferring it from throughput.
            var limit = _controller.Evaluate(_buffer.Reader.Count > 0);
            _applyLimit(limit);
            _recordEvaluation();
        }
    }

    private static async Task<TimeSpan> _probeThreadPoolAsync(CancellationToken ctk)
    {
        var started = Stopwatch.GetTimestamp();
        await Task.Run(static () => { }, ctk).ConfigureAwait(false);
        return Stopwatch.GetElapsedTime(started);
    }

    private void _applyLimit(int limit)
    {
        lock (_gate)
        {
            if (_processing is not { } processing || _workers is null)
                return;

            if (limit > _workerTarget)
            {
                var added = limit - _workerTarget;
                _workerTarget = limit;
                _shrinkStreak = 0;
                MessagingMetrics._recordConcurrencyLimit(added, _tags);
                _startWorkers(added, processing.Token);
            }
            else if (limit < _workerTarget)
            {
                // Hysteresis: one low reading is noise, two in a row is a trend. Without it the pool
                // churns workers on every oscillation of the controller.
                if (++_shrinkStreak < 2)
                    return;

                _shrinkStreak = 0;
                MessagingMetrics._recordConcurrencyLimit(limit - _workerTarget, _tags);
                _workerTarget = limit;
            }
            else
            {
                _shrinkStreak = 0;
                return;
            }

            _applyBudget(_options.ComputePrefetchBudget(_workerTarget, _source.ReceiverCapabilities));
        }
    }

    private void _applyBudget(int budget)
    {
        var previous = Volatile.Read(ref _prefetchBudget);
        if (budget == previous)
            return;

        Volatile.Write(ref _prefetchBudget, budget);
        if (budget > previous)
        {
            var grant = budget - previous;
            // Cancel outstanding debt before handing out new credit, or the budget grows twice.
            while (grant > 0)
            {
                var debt = Volatile.Read(ref _creditDebt);
                if (debt <= 0)
                    break;
                var repaid = Math.Min(debt, grant);
                if (Interlocked.CompareExchange(ref _creditDebt, debt - repaid, debt) == debt)
                    grant -= repaid;
            }

            if (grant > 0)
                _credits.Release(grant);

            return;
        }

        // Credits already handed out cannot be recalled, so the shortfall is booked as debt and
        // absorbed the next time a worker releases.
        var withdraw = previous - budget;
        var taken = 0;
        while (taken < withdraw && _credits.Wait(0, CancellationToken.None))
            taken++;
        if (taken < withdraw)
            Interlocked.Add(ref _creditDebt, withdraw - taken);
    }

    private async Task _acquireCreditAsync(CancellationToken ctk)
    {
        while (true)
        {
            await _credits.WaitAsync(ctk).ConfigureAwait(false);
            if (!_absorbIntoDebt())
            {
                Interlocked.Increment(ref _outstanding);
                return;
            }
        }
    }

    private bool _tryAcquireCredit()
    {
        if (!_credits.Wait(0, CancellationToken.None))
            return false;
        if (_absorbIntoDebt())
            return false;

        Interlocked.Increment(ref _outstanding);
        return true;
    }

    private void _releaseCredit(int count)
    {
        for (var index = 0; index < count; index++)
        {
            Interlocked.Decrement(ref _outstanding);
            if (!_absorbIntoDebt())
                _credits.Release();
        }
    }

    private bool _absorbIntoDebt()
    {
        while (true)
        {
            var debt = Volatile.Read(ref _creditDebt);
            if (debt <= 0)
                return false;
            if (Interlocked.CompareExchange(ref _creditDebt, debt - 1, debt) == debt)
                return true;
        }
    }

    private static async Task _delayAsync(TimeSpan delay, CancellationToken ctk)
    {
        if (delay <= TimeSpan.Zero)
            return;

        await Task.Delay(delay, ctk).ConfigureAwait(false);
    }

    private async Task _workAsync(CancellationToken ctk)
    {
        // The count must match the workers that actually exist: an exit that forgets to decrement it
        // makes the pool look bigger than it is, and shrinking then retires workers that are gone.
        var retired = false;
        try
        {
            retired = await _dispatchLoopAsync(ctk).ConfigureAwait(false);
        }
        finally
        {
            if (!retired)
                Interlocked.Decrement(ref _activeWorkers);
        }
    }

    /// <summary>Dispatches buffered deliveries until the buffer completes or the worker retires.</summary>
    /// <param name="ctk">The processing cancellation token.</param>
    /// <returns><see langword="true"/> when the worker retired and already gave up its slot.</returns>
    private async Task<bool> _dispatchLoopAsync(CancellationToken ctk)
    {
        while (await _buffer.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            if (!_buffer.Reader.TryRead(out var buffered))
                continue;

            var delivery = buffered.Delivery;
            MessagingMetrics._recordBuffered(-1, _tags);
            MessagingMetrics._recordInFlight(1, _tags);

            // A lost lock must cancel the handler: continuing would settle a delivery the broker has
            // already handed to somebody else.
            var started = Stopwatch.GetTimestamp();
            if (buffered.EnqueuedTicks != 0)
                MessagingMetrics._recordQueueWait(Stopwatch.GetElapsedTime(buffered.EnqueuedTicks, started), _tags);

            var renewedDelivery = delivery as IMessagingRenewedDelivery;
            Exception? settleFailure = null;
            using var handlerCancellation = delivery is IMessagingRenewedDelivery renewed
                ? CancellationTokenSource.CreateLinkedTokenSource(ctk, renewed._lockLost)
                : CancellationTokenSource.CreateLinkedTokenSource(ctk);
            try
            {
                // Settlement happens inside the callback, so releasing credit afterwards releases it
                // after settlement.
                await _onDelivery(delivery, handlerCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ctk.IsCancellationRequested)
            {
                await _settleQuietlyAsync(delivery).ConfigureAwait(false);
                MessagingMetrics._recordInFlight(-1, _tags);
                _releaseCredit(1);
                return false;
            }
#pragma warning disable CA1031, ERP022 // A worker must survive a settlement or broker failure.
            catch (Exception exception)
            {
                settleFailure = exception;
                _logger.Warn(
                    exception,
                    CultureInfo.InvariantCulture,
                    "Dispatch or settlement failed for delivery {deliveryId} on queue {queue}; abandoning it for redelivery.",
                    delivery.DeliveryId,
                    _queue);
                await _settleQuietlyAsync(delivery).ConfigureAwait(false);
            }
#pragma warning restore CA1031, ERP022

            if (settleFailure is MessagingThrottledException)
            {
                MessagingMetrics._recordThrottled(_tags);
                _reportSignal(MessagingConcurrencySignal.BrokerThrottled);
            }

            var lockLost = settleFailure is MessagingLockLostException
                || (renewedDelivery is not null && renewedDelivery._lockLost.IsCancellationRequested);
            MessagingMetrics._recordInFlight(-1, _tags);
            _releaseCredit(1);
            _controller.ReportCompletion(Stopwatch.GetElapsedTime(started));
            if (lockLost)
                _reportSignal(MessagingConcurrencySignal.LockLost);

            // Shrinking is cooperative: a worker leaves after its delivery settled, never mid-flight.
            if (_tryRetireWorker())
                return true;
        }

        return false;
    }

    private void _reportSignal(MessagingConcurrencySignal signal)
    {
        _controller.ReportSignal(signal);
        if (_options.AdvancedMetrics)
            MessagingMetrics._recordConcurrencyDecision(_withReason(_reason(signal)));
    }

    private void _recordEvaluation()
    {
        // The gate is checked before reading the controller, so a disabled tier costs one bool.
        if (!_options.AdvancedMetrics || _controller is not IMessagingConcurrencyDiagnostics diagnostics)
            return;

        var decision = diagnostics.LastDecision;
        MessagingMetrics._recordGradient(decision.Gradient, _tags);
        if (decision.Reason is { } reason)
            MessagingMetrics._recordConcurrencyDecision(_withReason(reason));
    }

    private KeyValuePair<string, object?>[] _withReason(string reason)
    {
        var tags = new KeyValuePair<string, object?>[_tags.Length + 1];
        _tags.CopyTo(tags, 0);
        tags[^1] = new KeyValuePair<string, object?>("reason", reason);
        return tags;
    }

    private static string _reason(MessagingConcurrencySignal signal)
    {
        return signal switch
        {
            MessagingConcurrencySignal.BrokerThrottled => "throttled",
            MessagingConcurrencySignal.LockLost => "lock_lost",
            MessagingConcurrencySignal.HandlerTimeout => "timeout",
            MessagingConcurrencySignal.ThreadPoolStarved => "starvation",
            _ => "backpressure"
        };
    }

    /// <summary>A buffered delivery and, when the advanced tier is on, the timestamp it was buffered at.</summary>
    private readonly record struct BufferedDelivery(IMessagingLockedDelivery Delivery, long EnqueuedTicks);

    private static async Task _settleQuietlyAsync(IMessagingLockedDelivery delivery)
    {
        try
        {
            await delivery.AbandonAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031, ERP022 // The lock may already be lost; there is nothing left to do.
        catch (Exception exception)
        {
            // Debug, not warning: losing the lock before abandoning it is the expected outcome of a
            // failed renewal, and the message is redelivered either way. Silence would make a
            // genuinely broken transport indistinguishable from that.
            _logger.Debug(
                exception,
                CultureInfo.InvariantCulture,
                "Abandoning delivery {deliveryId} failed; the lock will expire instead.",
                delivery.DeliveryId);
        }
#pragma warning restore CA1031, ERP022
    }

    private static async Task<bool> _waitAsync(Task task, TimeSpan timeout, CancellationToken ctk)
    {
        try
        {
#pragma warning disable VSTHRD003 // The workers are intentionally started on the thread pool.
            await task.WaitAsync(timeout, ctk).ConfigureAwait(false);
#pragma warning restore VSTHRD003
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task _observeAsync(Task? task)
    {
        if (task is null)
            return;

        try
        {
#pragma warning disable VSTHRD003 // The loop is intentionally started on the thread pool.
            await task.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the normal way the receive loop ends.
        }
    }
}
