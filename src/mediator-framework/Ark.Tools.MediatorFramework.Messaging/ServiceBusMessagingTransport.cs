// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Azure Service Bus implementation of the messaging transport contract.</summary>
public sealed class ServiceBusMessagingTransport : IMessagingReceiveTransport, IAsyncDisposable
{
    private const int _amqpPropertyOverheadBytes = 8;
    private const int _maximumDeadLetterReasonLength = 256;
    private const int _maximumDeadLetterDescriptionLength = 1_024;
    private const long _maximumMessageBytes = 256 * 1024;

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
        MessagingCapabilities.Receive
        | MessagingCapabilities.PubSub
        | MessagingCapabilities.ScheduledSend;

    /// <inheritdoc />
    public long? MaximumInlineEnvelopeBytes => _maximumMessageBytes;

    /// <inheritdoc />
    public long MeasureNative(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var size = payload.Length;
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
        queue = MessagingEntityNameMapper.ToServiceBus(queue);
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
        topic = MessagingEntityNameMapper.ToServiceBus(topic);
        ArgumentNullException.ThrowIfNull(headers);
        _validateSize(headers, payload);

        var sender = _senders.GetOrAdd(
            topic,
            static (entity, client) => client.CreateSender(entity),
            _client);
        await sender.SendMessageAsync(_toNativeMessage(headers, payload), ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(
        string queue,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        queue = MessagingEntityNameMapper.ToServiceBus(queue);
        return _receiveAsync(queue, ctk);
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

    private async IAsyncEnumerable<IMessagingLockedDelivery> _receiveAsync(
        string queue,
        [EnumeratorCancellation] CancellationToken ctk)
    {
        var receiver = _receivers.GetOrAdd(
            queue,
            static (entity, client) => client.CreateReceiver(
                entity,
                new ServiceBusReceiverOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.PeekLock
                }),
            _client);
        while (true)
        {
            ctk.ThrowIfCancellationRequested();
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(1), ctk)
                .ConfigureAwait(false);
            if (message is not null)
                yield return new ServiceBusLockedDelivery(receiver, message);
        }
    }

    private static ServiceBusMessage _toNativeMessage(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        var buffer = new ArrayBufferWriter<byte>(Math.Max(checked((int)payload.Length), 1));
        foreach (var segment in payload)
            buffer.Write(segment.Span);

        var message = new ServiceBusMessage(BinaryData.FromBytes(buffer.WrittenMemory));
        foreach (var pair in headers)
            message.ApplicationProperties.Add(pair.Key, pair.Value);

        return message;
    }

    private void _validateSize(
        IReadOnlyDictionary<string, string> headers,
        in ReadOnlySequence<byte> payload)
    {
        if (MeasureNative(headers, payload) > _maximumMessageBytes)
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
