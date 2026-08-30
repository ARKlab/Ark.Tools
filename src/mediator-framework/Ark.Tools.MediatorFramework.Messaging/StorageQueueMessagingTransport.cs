// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Azure Storage Queue implementation of the messaging transport contract.</summary>
public sealed class StorageQueueMessagingTransport :
    IMessagingReceiveTransport,
    IMessagingTransportManagement
{
    private static readonly TimeSpan _maximumVisibilityDelay = TimeSpan.FromDays(7);
    private readonly QueueServiceClient _serviceClient;
    private readonly ConcurrentDictionary<string, QueueClient> _queues = new(StringComparer.Ordinal);
    private readonly TimeSpan _receiveVisibilityTimeout;
    private readonly TimeSpan _retryDelay;

    /// <summary>Creates a transport over an application-composed Queue Storage service client.</summary>
    /// <param name="serviceClient">The Queue Storage service client configured with no message encoding.</param>
    /// <param name="receiveVisibilityTimeout">The visibility window used by the custom receive pump.</param>
    /// <param name="retryDelay">The delay applied when the custom receive pump abandons a delivery.</param>
    public StorageQueueMessagingTransport(
        QueueServiceClient serviceClient,
        TimeSpan? receiveVisibilityTimeout = null,
        TimeSpan? retryDelay = null)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        _receiveVisibilityTimeout = receiveVisibilityTimeout ?? TimeSpan.FromMinutes(1);
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
        _validateVisibility(_receiveVisibilityTimeout, nameof(receiveVisibilityTimeout));
        _validateVisibility(_retryDelay, nameof(retryDelay));
    }

    /// <summary>Creates a connection-string transport with Azure Queue message encoding disabled.</summary>
    /// <param name="connectionString">The Queue Storage connection string.</param>
    /// <param name="receiveVisibilityTimeout">The visibility window used by the custom receive pump.</param>
    /// <param name="retryDelay">The delay applied when the custom receive pump abandons a delivery.</param>
    public StorageQueueMessagingTransport(
        string connectionString,
        TimeSpan? receiveVisibilityTimeout = null,
        TimeSpan? retryDelay = null)
        : this(
            new QueueServiceClient(connectionString, _clientOptions()),
            receiveVisibilityTimeout,
            retryDelay)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
    }

    /// <summary>Creates a managed-identity transport with Azure Queue message encoding disabled.</summary>
    /// <param name="serviceUri">The Queue Storage service endpoint.</param>
    /// <param name="credential">The managed identity or token credential.</param>
    /// <param name="receiveVisibilityTimeout">The visibility window used by the custom receive pump.</param>
    /// <param name="retryDelay">The delay applied when the custom receive pump abandons a delivery.</param>
    public StorageQueueMessagingTransport(
        Uri serviceUri,
        TokenCredential credential,
        TimeSpan? receiveVisibilityTimeout = null,
        TimeSpan? retryDelay = null)
        : this(
            new QueueServiceClient(serviceUri, credential, _clientOptions()),
            receiveVisibilityTimeout,
            retryDelay)
    {
        ArgumentNullException.ThrowIfNull(serviceUri);
        ArgumentNullException.ThrowIfNull(credential);
    }

    /// <inheritdoc />
    public MessagingCapabilities Capabilities =>
        MessagingCapabilities.Receive | MessagingCapabilities.ScheduledSend;

    /// <inheritdoc />
    public long? MaximumInlineEnvelopeBytes =>
        Base64.GetMaxEncodedToUtf8Length(StorageQueueLimits.MaximumNormalCanonicalBytes);

    /// <inheritdoc />
    public long? GetMaximumInlinePayloadBytes(IReadOnlyDictionary<string, string> headers)
    {
        return StorageQueueLimits.MaximumNormalCanonicalBytes
            - StorageQueueEnvelopeCodec._measureCanonical(headers, ReadOnlySequence<byte>.Empty);
    }

    /// <inheritdoc />
    public long MeasureNative(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        var canonicalBytes = StorageQueueEnvelopeCodec._measureCanonical(headers, payload);
        return Base64.GetMaxEncodedToUtf8Length(canonicalBytes);
    }

    /// <inheritdoc />
    public async Task SendAsync(
        string queue,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        DateTimeOffset? dueTime,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentNullException.ThrowIfNull(headers);

        var encoded = StorageQueueEnvelopeCodec.Encode(headers, payload);
        var visibilityDelay = _scheduledDelay(dueTime);
        await _queue(queue).SendMessageAsync(
            BinaryData.FromString(encoded),
            visibilityDelay,
            timeToLive: null,
            ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        string topic,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        CancellationToken ctk)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException(
            "Azure Storage Queue does not support the PubSub messaging capability.");
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(
        string queue,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        return _receiveAsync(queue, ctk);
    }

    /// <inheritdoc />
    public async Task EnsureQueueAsync(
        string queue,
        int maximumDeliveryCount,
        string ownerIdentity,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        await _queue(queue).CreateIfNotExistsAsync(cancellationToken: ctk).ConfigureAwait(false);
        await _queue(_poisonQueue(queue)).CreateIfNotExistsAsync(cancellationToken: ctk)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnsureTopicAsync(
        string topic,
        string ownerIdentity,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(ownerIdentity);
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException(
            "Azure Storage Queue does not support the PubSub messaging capability.");
    }

    /// <inheritdoc />
    public async Task EnsureSubscriptionAsync(
        MessagingSubscriptionResource subscription,
        CancellationToken ctk)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException(
            "Azure Storage Queue does not support the PubSub messaging capability.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MessagingTransportSubscription>> GetSubscriptionsAsync(
        string topic,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException(
            "Azure Storage Queue does not support the PubSub messaging capability.");
    }

    /// <inheritdoc />
    public async Task DeleteSubscriptionAsync(
        string topic,
        string subscription,
        CancellationToken ctk)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException(
            "Azure Storage Queue does not support the PubSub messaging capability.");
    }

    private async IAsyncEnumerable<IMessagingLockedDelivery> _receiveAsync(
        string queue,
        [EnumeratorCancellation] CancellationToken ctk)
    {
        var source = _queue(queue);
        var poison = _queue(_poisonQueue(queue));
        while (true)
        {
            ctk.ThrowIfCancellationRequested();
            var response = await source.ReceiveMessagesAsync(
                maxMessages: 1,
                visibilityTimeout: _receiveVisibilityTimeout,
                cancellationToken: ctk).ConfigureAwait(false);
            if (response.Value.Length == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), ctk).ConfigureAwait(false);
                continue;
            }

            var message = response.Value[0];
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
                    ctk).ConfigureAwait(false);
                continue;
            }

            yield return new StorageQueueLockedDelivery(
                source,
                poison,
                message,
                envelope,
                _receiveVisibilityTimeout,
                _retryDelay);
        }
    }

    private QueueClient _queue(string queue)
    {
        return _queues.GetOrAdd(
            queue,
            static (name, client) => client.GetQueueClient(name),
            _serviceClient);
    }

    private static QueueClientOptions _clientOptions()
    {
        return new QueueClientOptions
        {
            MessageEncoding = QueueMessageEncoding.None
        };
    }

    private static TimeSpan? _scheduledDelay(DateTimeOffset? dueTime)
    {
        if (dueTime is null)
            return null;

        var delay = dueTime.Value - DateTimeOffset.UtcNow;
        if (delay <= TimeSpan.Zero)
            return null;
        if (delay > _maximumVisibilityDelay)
            throw new ArgumentOutOfRangeException(
                nameof(dueTime),
                "Azure Storage Queue scheduled delivery cannot exceed seven days.");
        return delay;
    }

    private static void _validateVisibility(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero || value > _maximumVisibilityDelay)
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Azure Storage Queue visibility delays must be positive and no greater than seven days.");
    }

    private static string _poisonQueue(string queue)
    {
        return queue + "-poison";
    }

    private static async Task _moveToPoisonAsync(
        QueueClient source,
        QueueClient poison,
        QueueMessage message,
        string reason,
        string description,
        CancellationToken ctk)
    {
        var body = StorageQueueEnvelopeCodec.EncodePoison(
            message.Body,
            message.MessageId,
            reason,
            description);
        await poison.SendMessageAsync(BinaryData.FromString(body), cancellationToken: ctk)
            .ConfigureAwait(false);
        await source.DeleteMessageAsync(message.MessageId, message.PopReceipt, ctk)
            .ConfigureAwait(false);
    }

    private sealed class StorageQueueLockedDelivery : IMessagingLockedDelivery
    {
        private readonly QueueClient _source;
        private readonly QueueClient _poison;
        private readonly QueueMessage _message;
        private readonly TimeSpan _receiveVisibilityTimeout;
        private readonly TimeSpan _retryDelay;
        private string _popReceipt;

        public StorageQueueLockedDelivery(
            QueueClient source,
            QueueClient poison,
            QueueMessage message,
            StorageQueueEnvelope envelope,
            TimeSpan receiveVisibilityTimeout,
            TimeSpan retryDelay)
        {
            _source = source;
            _poison = poison;
            _message = message;
            _popReceipt = message.PopReceipt;
            _receiveVisibilityTimeout = receiveVisibilityTimeout;
            _retryDelay = retryDelay;
            Headers = envelope.Headers;
            Payload = envelope.Payload;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public ReadOnlySequence<byte> Payload { get; }

        public int DeliveryCount => checked((int)_message.DequeueCount);

        public async Task RenewLockAsync(CancellationToken ctk)
        {
            var response = await _source.UpdateMessageAsync(
                _message.MessageId,
                _popReceipt,
                _message.Body,
                _receiveVisibilityTimeout,
                ctk).ConfigureAwait(false);
            _popReceipt = response.Value.PopReceipt;
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            await _source.DeleteMessageAsync(_message.MessageId, _popReceipt, ctk)
                .ConfigureAwait(false);
        }

        public async Task AbandonAsync(CancellationToken ctk)
        {
            var response = await _source.UpdateMessageAsync(
                _message.MessageId,
                _popReceipt,
                _message.Body,
                _retryDelay,
                ctk).ConfigureAwait(false);
            _popReceipt = response.Value.PopReceipt;
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
