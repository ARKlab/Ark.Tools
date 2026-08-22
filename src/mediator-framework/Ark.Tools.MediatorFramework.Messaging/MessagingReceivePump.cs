// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Runs a long-lived receive loop for tests and custom hosts.</summary>
public sealed class MessagingReceivePump : IAsyncDisposable
{
    private readonly IMessagingReceiveTransport _transport;
    private readonly string _queue;
    private readonly Func<IMessagingLockedDelivery, CancellationToken, Task> _onDelivery;
    private readonly Lock _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private bool _stopping;

    /// <summary>Creates a receive pump.</summary>
    /// <param name="transport">The receive-capable transport.</param>
    /// <param name="queue">The source queue.</param>
    /// <param name="onDelivery">The callback invoked for each locked delivery.</param>
    public MessagingReceivePump(
        IMessagingReceiveTransport transport,
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentNullException.ThrowIfNull(onDelivery);
        _transport = transport;
        _queue = queue;
        _onDelivery = onDelivery;
    }

    /// <summary>Starts the receive loop.</summary>
    /// <param name="ctk">The host cancellation token.</param>
    /// <returns>A task completed once the loop has started.</returns>
    public Task StartAsync(CancellationToken ctk)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        lock (_gate)
        {
            if (_cts is not null || _stopping)
            {
                cts.Dispose();
                throw new InvalidOperationException("The messaging receive pump has already been started or is stopping.");
            }

            _cts = cts;
            _loop = Task.Run(() => _runAsync(cts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    /// <summary>Stops the receive loop and observes any loop failure.</summary>
    /// <returns>A task completed when the loop has stopped.</returns>
    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            if (cts is null)
                return;

            _cts = null;
            _loop = null;
            _stopping = true;
        }

        using var ctsToDispose = cts;
        try
        {
            await cts.CancelAsync().ConfigureAwait(false);
            if (loop is not null)
            {
#pragma warning disable VSTHRD003 // The receive loop is intentionally started on the thread pool.
                await loop.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Cancellation is expected when stopping the receive loop.
        }
        finally
        {
            lock (_gate)
                _stopping = false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task _runAsync(CancellationToken ctk)
    {
        await foreach (var delivery in _transport.ReceiveAsync(_queue, ctk).ConfigureAwait(false))
            await _onDelivery(delivery, ctk).ConfigureAwait(false);
    }
}
