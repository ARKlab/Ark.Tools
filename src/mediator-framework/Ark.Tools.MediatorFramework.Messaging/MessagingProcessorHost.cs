// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Threading.Channels;

using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Processes one participant queue with a bounded buffer and a pool of async workers.</summary>
/// <remarks>
/// The host owns the receive loop: it computes available credit, receives a batch no larger than the
/// transport's maximum, writes deliveries into a bounded channel, and lets workers dispatch them
/// concurrently. Credit is released only after settlement, so a slow settle cannot cause over-fetching.
/// </remarks>
public sealed class MessagingProcessorHost : IHostedService, IAsyncDisposable
{
    private readonly IMessagingMessageSource _source;
    private readonly Func<IMessagingLockedDelivery, CancellationToken, Task> _onDelivery;
    private readonly string _queue;
    private readonly MessagingProcessingOptions _options;
    private readonly Channel<IMessagingLockedDelivery> _buffer;
    private readonly SemaphoreSlim _credits;
    private readonly int _prefetchBudget;
    private readonly int _batchSize;
    private readonly Lock _gate = new();
    private CancellationTokenSource? _receiving;
    private CancellationTokenSource? _processing;
    private Task[]? _workers;
    private Task? _receiveLoop;

    /// <summary>Creates a processor host for one queue.</summary>
    /// <param name="source">The pull message source.</param>
    /// <param name="queue">The participant queue to process.</param>
    /// <param name="onDelivery">The per-delivery callback, normally <c>MessagingDispatcher.OnDeliveryAsync</c>.</param>
    /// <param name="options">The processing options, or <see langword="null"/> for the defaults.</param>
    public MessagingProcessorHost(
        IMessagingMessageSource source,
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery,
        MessagingProcessingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentNullException.ThrowIfNull(onDelivery);

        _source = source;
        _queue = queue;
        _onDelivery = onDelivery;
        _options = options ?? new MessagingProcessingOptions();
        _options.Validate();

        // ponytail: the concurrency limit is fixed at InitialConcurrency, so the budget is computed
        // once. Ceiling: no adaptation to load; AMF-05 makes the limit adaptive and recomputes the
        // budget whenever it changes.
        Concurrency = _options.InitialConcurrency;
        _prefetchBudget = _options.ComputePrefetchBudget(Concurrency, source.ReceiverCapabilities);
        _batchSize = Math.Min(_prefetchBudget, source.ReceiverCapabilities.MaximumBatchSize);
        _credits = new SemaphoreSlim(_prefetchBudget, _prefetchBudget);
        _buffer = Channel.CreateBounded<IMessagingLockedDelivery>(new BoundedChannelOptions(_prefetchBudget)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });
    }

    /// <summary>Gets the number of workers dispatching deliveries.</summary>
    public int Concurrency { get; }

    /// <summary>Gets the maximum number of deliveries that may be buffered plus in flight.</summary>
    public int PrefetchBudget => _prefetchBudget;

    /// <summary>Gets the number of deliveries currently buffered or in flight.</summary>
    public int Outstanding => _prefetchBudget - _credits.CurrentCount;

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
            _workers = new Task[Concurrency];
            for (var index = 0; index < Concurrency; index++)
                _workers[index] = Task.Run(() => _workAsync(processing.Token), CancellationToken.None);
            _receiveLoop = Task.Run(() => _receiveAsync(receiving.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? receiving;
        CancellationTokenSource? processing;
        Task[]? workers;
        Task? receiveLoop;
        lock (_gate)
        {
            receiving = _receiving;
            processing = _processing;
            workers = _workers;
            receiveLoop = _receiveLoop;
            _receiving = null;
            _processing = null;
            _workers = null;
            _receiveLoop = null;
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
        var drain = Task.WhenAll(workers ?? []);
        var drained = await _waitAsync(drain, _options.ShutdownTimeout, cancellationToken).ConfigureAwait(false);
        if (!drained)
        {
            await processing.CancelAsync().ConfigureAwait(false);
            await _observeAsync(drain).ConfigureAwait(false);
        }

        // Abandon whatever never got processed so redelivery is immediate rather than
        // lock-expiry-delayed.
        while (_buffer.Reader.TryRead(out var delivery))
        {
            AbandonedOnShutdown++;
            await _settleQuietlyAsync(delivery).ConfigureAwait(false);
            _credits.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _credits.Dispose();
    }

    private async Task _receiveAsync(CancellationToken ctk)
    {
        while (!ctk.IsCancellationRequested)
        {
            // Acquiring credit before receiving is the backpressure: a full budget blocks here and
            // no receive call is made at all.
            await _credits.WaitAsync(ctk).ConfigureAwait(false);
            var requested = 1;
            while (requested < _batchSize && _credits.Wait(0, CancellationToken.None))
                requested++;

            var received = 0;
            try
            {
                var batch = await _source
                    .ReceiveBatchAsync(_queue, requested, _options.ReceiveWaitTime, ctk)
                    .ConfigureAwait(false);
                foreach (var delivery in batch)
                {
                    received++;
                    await _buffer.Writer.WriteAsync(delivery, ctk).ConfigureAwait(false);
                }
            }
            finally
            {
                // Credit is only consumed by deliveries actually taken from the broker.
                if (requested > received)
                    _credits.Release(requested - received);
            }
        }
    }

    private async Task _workAsync(CancellationToken ctk)
    {
        while (await _buffer.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            if (!_buffer.Reader.TryRead(out var delivery))
                continue;

            try
            {
                // Settlement happens inside the callback, so releasing credit afterwards releases it
                // after settlement.
                await _onDelivery(delivery, ctk).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ctk.IsCancellationRequested)
            {
                await _settleQuietlyAsync(delivery).ConfigureAwait(false);
                _credits.Release();
                return;
            }
#pragma warning disable CA1031, ERP022 // A worker must survive a settlement or broker failure.
            catch (Exception)
            {
                // ponytail: the delivery is abandoned for immediate redelivery and the worker keeps
                // going. Ceiling: the failure is not reported anywhere; AMF-09 adds the metric and
                // the structured log for it.
                await _settleQuietlyAsync(delivery).ConfigureAwait(false);
            }
#pragma warning restore CA1031, ERP022

            _credits.Release();
        }
    }

    private static async Task _settleQuietlyAsync(IMessagingLockedDelivery delivery)
    {
        try
        {
            await delivery.AbandonAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031, ERP022 // The lock may already be lost; there is nothing left to do.
        catch (Exception)
        {
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
