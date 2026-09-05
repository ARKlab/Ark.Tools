// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Test-only sequential pump replacing the removed <c>MessagingReceivePump</c>.</summary>
internal static class MessagingSourceTestExtensions
{
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Receives exactly one delivery, polling until the timeout elapses.</summary>
    public static async Task<IMessagingLockedDelivery> ReceiveOneAsync(
        this IMessagingMessageSource source,
        string queue,
        TimeSpan? timeout = null,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctk);
        cts.CancelAfter(timeout ?? _defaultTimeout);
        while (true)
        {
            var batch = await source
                .ReceiveBatchAsync(queue, 1, TimeSpan.FromMilliseconds(100), cts.Token)
                .ConfigureAwait(false);
            if (batch.Count > 0)
                return batch[0];
        }
    }

    /// <summary>Starts a strictly sequential receive-and-dispatch loop.</summary>
    public static TestMessagePump StartPump(
        this IMessagingMessageSource source,
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery)
    {
        return new TestMessagePump(source, queue, onDelivery);
    }
}

/// <summary>A minimal sequential pump used by tests until <c>MessagingProcessorHost</c> exists.</summary>
internal sealed class TestMessagePump : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    internal TestMessagePump(
        IMessagingMessageSource source,
        string queue,
        Func<IMessagingLockedDelivery, CancellationToken, Task> onDelivery)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(onDelivery);
        var ctk = _cts.Token;
        _loop = Task.Run(
            async () =>
            {
                while (!ctk.IsCancellationRequested)
                {
                    var batch = await source
                        .ReceiveBatchAsync(queue, 1, TimeSpan.FromMilliseconds(100), ctk)
                        .ConfigureAwait(false);
                    foreach (var delivery in batch)
                        await onDelivery(delivery, ctk).ConfigureAwait(false);
                }
            },
            CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
#pragma warning disable VSTHRD003 // The loop is intentionally started on the thread pool.
            await _loop.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _cts.Dispose();
        }
    }
}
