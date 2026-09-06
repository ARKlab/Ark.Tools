// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

using Azure.Messaging.ServiceBus;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Azure Service Bus implementation of the messaging transport contract.</summary>
public sealed class ServiceBusMessagingTransport :
    IMessagingTransport,
    IMessagingMessageSource,
    IAsyncDisposable,
    IMessagingTransport<ServiceBusMessagingTransport>
{
    /// <summary>Gets the Service Bus standard-tier maximum complete payload size.</summary>
    public const long MaximumPayloadSizeBytes = 256 * 1024;
    static long IMessagingTransport<ServiceBusMessagingTransport>.MaximumPayloadLimitBytes =>
        MaximumPayloadSizeBytes;
    private const int _amqpPropertyOverheadBytes = 8;
    private const int _maximumDeadLetterReasonLength = 256;
    private const int _maximumDeadLetterDescriptionLength = 1_024;

    private readonly ServiceBusClient _client;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ServiceBusReceiver> _receivers = new(StringComparer.Ordinal);

    /// <summary>Creates the transport over an application-composed client.</summary>
    /// <param name="client">The Service Bus client owned by this transport.</param>
    public ServiceBusMessagingTransport(ServiceBusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc />
    public MessagingCapabilities Capabilities =>
        MessagingCapabilities.SendReceive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend;

    /// <inheritdoc />
    public long MaximumPayloadBytes => MaximumPayloadSizeBytes;

    /// <summary>Maps a logical name to a Service Bus entity name.</summary>
    public static string ToNativeEntityName(string logicalName)
    {
        return MessagingEntityNameMapper.ToServiceBus(logicalName);
    }

    static long IMessagingTransport<ServiceBusMessagingTransport>.GetNativeHeaderSize(
        IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var size = 0L;
        checked
        {
            foreach (var pair in headers)
                size += Encoding.UTF8.GetByteCount(pair.Key)
                    + Encoding.UTF8.GetByteCount(pair.Value)
                    + _amqpPropertyOverheadBytes;
        }

        return size;
    }

    /// <inheritdoc />
    public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var size = 0L;
        checked
        {
            foreach (var pair in headers)
            {
                size += Encoding.UTF8.GetByteCount(pair.Key)
                    + Encoding.UTF8.GetByteCount(pair.Value)
                    + _amqpPropertyOverheadBytes;
            }
        }

        return size;
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
        queue = ToNativeEntityName(queue);
        ArgumentNullException.ThrowIfNull(headers);
        _validateSize(headers, payload);

        var sender = _senders.GetOrAdd(
            queue,
            static (entity, client) => client.CreateSender(entity),
            _client);
        var message = _toNativeMessage(headers, payload);
        if (dueTime is { } scheduled)
            _ = await sender.ScheduleMessageAsync(message, scheduled, ctk).ConfigureAwait(false);
        else
            await sender.SendMessageAsync(message, ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        string topic,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        topic = ToNativeEntityName(topic);
        ArgumentNullException.ThrowIfNull(headers);
        _validateSize(headers, payload);

        var sender = _senders.GetOrAdd(
            topic,
            static (entity, client) => client.CreateSender(entity),
            _client);
        await sender.SendMessageAsync(_toNativeMessage(headers, payload), ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public MessagingReceiverCapabilities ReceiverCapabilities => new(
        MaximumBatchSize: MaximumReceiveBatchSize,
        SupportsServerSideWait: true,
        SupportsLockRenewal: true,
        NativeLockDuration: null);

    /// <summary>Gets the maximum number of messages a single receive returns.</summary>
    /// <remarks>
    /// One at this task: batch receive over <c>ReceiveMessagesAsync</c> lands in AMF-07.
    /// </remarks>
    public const int MaximumReceiveBatchSize = 1;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
        string queue,
        int maxMessages,
        TimeSpan maxWait,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWait.Ticks, 0, nameof(maxWait));

        var receiver = _receiver(ToNativeEntityName(queue));
        var message = await receiver.ReceiveMessageAsync(maxWait, ctk).ConfigureAwait(false);
        return message is null
            ? Array.Empty<IMessagingLockedDelivery>()
            : [new ServiceBusLockedDelivery(receiver, message)];
    }

    private ServiceBusReceiver _receiver(string queue)
    {
        return _receivers.GetOrAdd(
            queue,
            static (entity, client) => client.CreateReceiver(
                entity,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock
                }),
            _client);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var receiver in _receivers.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            await receiver.Value.DisposeAsync().ConfigureAwait(false);
        _receivers.Clear();
        foreach (var sender in _senders.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            await sender.Value.DisposeAsync().ConfigureAwait(false);
        _senders.Clear();
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    private static ServiceBusMessage _toNativeMessage(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        var body = payload.IsSingleSegment
            ? BinaryData.FromBytes(payload.First)
            : BinaryData.FromBytes(payload.ToArray());
        var message = new ServiceBusMessage(body);
        foreach (var pair in headers)
            message.ApplicationProperties.Add(pair.Key, pair.Value);

        return message;
    }

    private void _validateSize(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        if (MeasureNativeHeaders(headers) + payload.Length > MaximumPayloadSizeBytes)
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                "The completed Service Bus message exceeds the 256 KB standard-tier limit.");
    }

    private sealed class ServiceBusLockedDelivery : IMessagingLockedDelivery
    {
        private readonly ServiceBusReceiver _receiver;
        private readonly ServiceBusReceivedMessage _message;
        private readonly IReadOnlyDictionary<string, string> _headers;

        public ServiceBusLockedDelivery(
            ServiceBusReceiver receiver,
            ServiceBusReceivedMessage message)
        {
            _receiver = receiver;
            _message = message;
            _headers = new ReadOnlyDictionary<string, string>(
                message.ApplicationProperties.ToDictionary(
                    static pair => pair.Key,
                    static pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                    StringComparer.Ordinal));
        }

        public IReadOnlyDictionary<string, string> Headers => _headers;

        public ReadOnlySequence<byte> Payload => new(_message.Body.ToMemory());

        public int DeliveryCount => _message.DeliveryCount;

        public string DeliveryId => _message.MessageId;

        public DateTimeOffset? LockedUntil => _message.LockedUntil;

        public async Task RenewLockAsync(CancellationToken ctk)
        {
            await _receiver.RenewMessageLockAsync(_message, ctk).ConfigureAwait(false);
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            await _receiver.CompleteMessageAsync(_message, ctk).ConfigureAwait(false);
        }

        public async Task AbandonAsync(CancellationToken ctk)
        {
            await _receiver.AbandonMessageAsync(
                _message,
                cancellationToken: ctk).ConfigureAwait(false);
        }

        public async Task DeadLetterAsync(
            string reason,
            string description,
            CancellationToken ctk)
        {
            await _receiver.DeadLetterMessageAsync(
                _message,
                deadLetterReason: _bound(reason, _maximumDeadLetterReasonLength),
                deadLetterErrorDescription: _bound(description, _maximumDeadLetterDescriptionLength),
                cancellationToken: ctk).ConfigureAwait(false);
        }

        private static string _bound(string value, int maximumLength)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.Length <= maximumLength ? value : value[..maximumLength];
        }
    }
}
