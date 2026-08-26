// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Maps Azure Functions QueueTrigger semantics onto the transport-neutral dispatcher.</summary>
public static class MessagingQueueFunctionsDispatcher
{
    /// <summary>Dispatches one Queue Storage delivery using host-owned settlement.</summary>
    /// <param name="message">The received Queue Storage message.</param>
    /// <param name="queue">The participant identity queue.</param>
    /// <param name="functionContext">The current Functions invocation context.</param>
    /// <param name="cancellationToken">The host cancellation token.</param>
    /// <returns>A task that completes after dispatch and settlement.</returns>
    public static async Task DispatchAsync(
        QueueMessage message,
        string queue,
        FunctionContext functionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentNullException.ThrowIfNull(functionContext);

        var services = functionContext.InstanceServices;
        var dispatcher = services.GetRequiredService<MessagingDispatcher>();
        var queueService = services.GetRequiredService<QueueServiceClient>();
        var source = queueService.GetQueueClient(queue);
        var poison = queueService.GetQueueClient(queue + "-poison");

        StorageQueueEnvelope envelope;
        try
        {
            envelope = StorageQueueEnvelopeCodec.Decode(message.Body);
        }
        catch (MessagingFailFastException exception)
        {
            await _moveToPoisonAsync(
                source,
                poison,
                message,
                exception.Reason.ToString(),
                exception.Message,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await dispatcher.OnDeliveryAsync(
            new StorageQueueFunctionsLockedDelivery(source, poison, message, envelope),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task _moveToPoisonAsync(
        QueueClient source,
        QueueClient poison,
        QueueMessage message,
        string reason,
        string description,
        CancellationToken cancellationToken)
    {
        var poisonBody = StorageQueueEnvelopeCodec.EncodePoison(
            message.Body,
            message.MessageId,
            reason,
            description);
        await poison.SendMessageAsync(
            BinaryData.FromString(poisonBody),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        await source.DeleteMessageAsync(
            message.MessageId,
            message.PopReceipt,
            cancellationToken).ConfigureAwait(false);
    }

    internal sealed class StorageQueueFunctionsLockedDelivery : IMessagingLockedDelivery
    {
        private readonly QueueClient _source;
        private readonly QueueClient _poison;
        private readonly QueueMessage _message;

        public StorageQueueFunctionsLockedDelivery(
            QueueClient source,
            QueueClient poison,
            QueueMessage message,
            StorageQueueEnvelope envelope)
        {
            _source = source;
            _poison = poison;
            _message = message;
            Headers = envelope.Headers;
            Payload = envelope.Payload;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public ReadOnlySequence<byte> Payload { get; }

        public int DeliveryCount => checked((int)_message.DequeueCount);

        public async Task RenewLockAsync(CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task AbandonAsync(CancellationToken ctk)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException(
                "The Storage Queue delivery was abandoned so the Functions host can apply its visibility timeout.");
        }

        public async Task DeadLetterAsync(
            string reason,
            string description,
            CancellationToken ctk)
        {
            await _moveToPoisonAsync(
                _source,
                _poison,
                _message,
                reason,
                description,
                ctk).ConfigureAwait(false);
        }
    }

}
