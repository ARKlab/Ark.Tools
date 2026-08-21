// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Runs a long-lived receive loop for tests and custom hosts.</summary>
public sealed class MessagingReceivePump : IAsyncDisposable
{
    private readonly IMessagingReceiveTransport _transport;
    private readonly string _queue;
    private readonly Func<IMessagingLockedDelivery, CancellationToken, Task> _onDelivery;
    private CancellationTokenSource? _cts;
    private Task? _loop;

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
        if (Interlocked.CompareExchange(ref _cts, cts, null) is not null)
        {
            cts.Dispose();
            throw new InvalidOperationException("The messaging receive pump has already been started.");
        }

        _loop = Task.Run(() => _runAsync(cts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Stops the receive loop and observes any loop failure.</summary>
    /// <returns>A task completed when the loop has stopped.</returns>
    public async Task StopAsync()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        if (cts is null)
            return;

        await cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_loop is not null)
            {
#pragma warning disable VSTHRD003 // The receive loop is intentionally started on the thread pool.
                await _loop.ConfigureAwait(false);
#pragma warning restore VSTHRD003
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
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
