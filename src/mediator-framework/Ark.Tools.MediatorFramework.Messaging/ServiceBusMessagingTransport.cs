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
    private readonly IReadOnlyList<ServiceBusClient> _clients;
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ServiceBusReceiver[]> _receivers = new(StringComparer.Ordinal);
    private readonly int _receiveChannels;
    private readonly int _maximumReceiveBatchSize;
    private readonly TimeSpan? _lockDuration;
    private int _nextChannel;

    /// <summary>Creates the transport over an application-composed client.</summary>
    /// <param name="client">The Service Bus client owned by this transport.</param>
    /// <param name="maximumReceiveBatchSize">
    /// An optional hard cap on a single receive. Defaults to <see langword="null"/>: Service Bus
    /// imposes no cap of its own, and the processor host already limits every request to its
    /// prefetch budget, which is the concurrency limit times the prefetch multiplier. Leaving this
    /// unset therefore keeps batches proportional to the configured parallelism and lets them grow
    /// only as adaptive concurrency raises the limit. Set it to pin a smaller ceiling.
    /// </param>
    /// <param name="receiveChannels">The number of receivers (AMQP links) opened per entity. Defaults to one.</param>
    /// <param name="lockDuration">The entity's lock duration, or <see langword="null"/> when unknown.</param>
    /// <param name="additionalClients">
    /// Extra clients to spread receive channels over, for rates a single AMQP connection cannot
    /// carry. Every client passed here is owned and disposed by this transport.
    /// </param>
    public ServiceBusMessagingTransport(
        ServiceBusClient client,
        int? maximumReceiveBatchSize = null,
        int receiveChannels = 1,
        TimeSpan? lockDuration = null,
        IReadOnlyList<ServiceBusClient>? additionalClients = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        if (maximumReceiveBatchSize is { } cap)
            ArgumentOutOfRangeException.ThrowIfLessThan(cap, 1, nameof(maximumReceiveBatchSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(receiveChannels, 1);
        if (lockDuration is { } duration)
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero, nameof(lockDuration));

        _maximumReceiveBatchSize = maximumReceiveBatchSize ?? int.MaxValue;
        _receiveChannels = receiveChannels;
        _lockDuration = lockDuration;
        _clients = additionalClients is null or { Count: 0 }
            ? [client]
            : [client, .. additionalClients];
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

    /// <summary>Gets the hard cap on a single receive, or <see cref="int.MaxValue"/> when uncapped.</summary>
    /// <remarks>
    /// Service Bus imposes no server-side cap. An uncapped transport is the conservative choice, not
    /// the aggressive one: the host never asks for more than its prefetch budget, so the batch is
    /// bounded by the configured parallelism and grows only with the adaptive concurrency limit. A
    /// fixed cap here can only make batches smaller than the host is already prepared to drain.
    /// </remarks>
    public int MaximumReceiveBatchSize => _maximumReceiveBatchSize;

    /// <inheritdoc />
    public MessagingReceiverCapabilities ReceiverCapabilities => new(
        MaximumBatchSize: _maximumReceiveBatchSize,
        SupportsServerSideWait: true,
        SupportsLockRenewal: true,
        NativeLockDuration: _lockDuration);

    /// <inheritdoc />
    /// <remarks>
    /// The SDK holds the request open for <paramref name="maxWait"/> and returns an empty list when it
    /// elapses, so an idle queue costs one held-open request instead of a poll loop.
    /// </remarks>
    public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
        string queue,
        int maxMessages,
        TimeSpan maxWait,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWait.Ticks, 0, nameof(maxWait));

        var entity = ToNativeEntityName(queue);
        var receiver = _receiver(entity);
        var requested = Math.Min(maxMessages, _maximumReceiveBatchSize);

        // Prefetch is 0, so the wait is the only thing keeping an idle receive from spinning; the SDK
        // rejects a non-positive window, and zero here means "ask once, do not wait".
        var wait = maxWait > TimeSpan.Zero ? maxWait : TimeSpan.FromMilliseconds(1);
        IReadOnlyList<ServiceBusReceivedMessage> messages;
        try
        {
            messages = await receiver.ReceiveMessagesAsync(requested, wait, ctk).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.ServiceBusy)
        {
            throw MessagingThrottledException._forEntity(entity, exception);
        }

        if (messages.Count == 0)
            return Array.Empty<IMessagingLockedDelivery>();

        var batch = new List<IMessagingLockedDelivery>(messages.Count);
        foreach (var message in messages)
            batch.Add(new ServiceBusLockedDelivery(receiver, message));

        return batch;
    }

    private ServiceBusReceiver _receiver(string queue)
    {
        var channels = _receivers.GetOrAdd(queue, _createReceivers);
        if (channels.Length == 1)
            return channels[0];

        // Round-robin over the links: each receiver holds its own credit, and Service Bus never hands
        // the same message to two links, so fan-out cannot double-deliver.
        var index = (uint)Interlocked.Increment(ref _nextChannel) % (uint)channels.Length;
        return channels[index];
    }

    private ServiceBusReceiver[] _createReceivers(string queue)
    {
        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock

            // PrefetchCount stays at its default of 0: the host's bounded buffer is the prefetch, and
            // unlike the AMQP one its locks are visible to the shared renewer.
        };
        var channels = new ServiceBusReceiver[_receiveChannels];
        for (var i = 0; i < channels.Length; i++)
            channels[i] = _clients[i % _clients.Count].CreateReceiver(queue, options);

        return channels;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var channels in _receivers.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var receiver in channels.Value)
                await receiver.DisposeAsync().ConfigureAwait(false);
        }

        _receivers.Clear();
        foreach (var sender in _senders.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            await sender.Value.DisposeAsync().ConfigureAwait(false);
        _senders.Clear();
        foreach (var client in _clients)
            await client.DisposeAsync().ConfigureAwait(false);
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

        public string DeliveryId => _message.LockToken;

        public DateTimeOffset? LockedUntil => _message.LockedUntil;

        public async Task RenewLockAsync(CancellationToken ctk)
        {
            await _callAsync(
                token => _receiver.RenewMessageLockAsync(_message, token),
                ctk).ConfigureAwait(false);
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            await _callAsync(
                token => _receiver.CompleteMessageAsync(_message, token),
                ctk).ConfigureAwait(false);
        }

        public async Task AbandonAsync(CancellationToken ctk)
        {
            await _callAsync(
                token => _receiver.AbandonMessageAsync(_message, cancellationToken: token),
                ctk).ConfigureAwait(false);
        }

        public async Task DeadLetterAsync(
            string reason,
            string description,
            CancellationToken ctk)
        {
            await _callAsync(
                token => _receiver.DeadLetterMessageAsync(
                    _message,
                    deadLetterReason: _bound(reason, _maximumDeadLetterReasonLength),
                    deadLetterErrorDescription: _bound(description, _maximumDeadLetterDescriptionLength),
                    cancellationToken: token),
                ctk).ConfigureAwait(false);
        }

        private async Task _callAsync(Func<CancellationToken, Task> call, CancellationToken ctk)
        {
            try
            {
                await call(ctk).ConfigureAwait(false);
            }
            catch (ServiceBusException exception)
                when (exception.Reason is ServiceBusFailureReason.MessageLockLost
                    or ServiceBusFailureReason.SessionLockLost)
            {
                throw MessagingLockLostException._forDelivery(_message.LockToken, exception);
            }
            catch (ServiceBusException exception)
                when (exception.Reason == ServiceBusFailureReason.ServiceBusy)
            {
                throw MessagingThrottledException._forEntity(_receiver.EntityPath, exception);
            }
        }

        private static string _bound(string value, int maximumLength)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.Length <= maximumLength ? value : value[..maximumLength];
        }
    }
}
