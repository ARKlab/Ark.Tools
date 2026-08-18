// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Rebus.Messages;
using Rebus.Transport;

using Ark.Tools.Core;

namespace Ark.Tools.Outbox.Rebus;

internal abstract class RebusOutboxProcessorCore : OutboxProcessorBase, IRebusOutboxProcessor, IDisposable
{
    private readonly ITransport _transport;
    private readonly CancellationTokenSource _busDisposalCancellationTokenSource = new();
    private Task _task = Task.CompletedTask;
    private bool _disposedValue;

    protected RebusOutboxProcessorCore(
        int topMessagesToRetrieve,
        ITransport transport)
        : base(topMessagesToRetrieve)
    {
        _transport = transport;
    }

    public void Start()
    {
        _task = ProcessLoopAsync(_busDisposalCancellationTokenSource.Token);
    }

    public void Stop()
    {
        _busDisposalCancellationTokenSource.Cancel();
#pragma warning disable VSTHRD002 // Sync wrapper for Stop method - wait for batch completion
        _task.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }

    protected override async Task ProcessMessagesAsync(
        IReadOnlyList<OutboxMessage> messages,
        CancellationToken ctk)
    {
        using var rebusTransactionScope = new RebusTransactionScope();
        foreach (var message in messages)
        {
            var destinationAddress = message.Headers?[OutboxTransportDecorator._outboxRecepientHeader];
            message.Headers?.Remove(OutboxTransportDecorator._outboxRecepientHeader);
            await _transport.Send(
                    destinationAddress!,
                    new TransportMessage(message.Headers, message.Body),
                    rebusTransactionScope.TransactionContext)
                .WithCancellation(ctk)
                .ConfigureAwait(false);
        }

        await rebusTransactionScope.CompleteAsync()
            .WithCancellation(ctk)
            .ConfigureAwait(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
                _busDisposalCancellationTokenSource.Dispose();

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
