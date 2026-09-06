// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;

using Azure;
using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Azure Storage Queue implementation of the messaging transport contract.</summary>
public sealed class StorageQueueMessagingTransport :
    IMessagingTransport,
    IMessagingMessageSource,
    IMessagingTransportManagement,
    IMessagingTransport<StorageQueueMessagingTransport>
{
    /// <summary>Gets the conservative post-Base64 UTF-8 byte limit for the queue message.</summary>
    public const long MaximumPayloadSizeBytes = 48 * 1024;
    static long IMessagingTransport<StorageQueueMessagingTransport>.MaximumPayloadLimitBytes =>
        MaximumPayloadSizeBytes;
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
        MessagingCapabilities.SendReceive | MessagingCapabilities.ScheduledSend;

    /// <inheritdoc />
    public long MaximumPayloadBytes => MaximumPayloadSizeBytes;

    /// <summary>Maps a logical name to a Storage Queue entity name.</summary>
    public static string ToNativeEntityName(string logicalName)
    {
        return MessagingEntityNameMapper.ToStorageQueue(logicalName);
    }

    static long IMessagingTransport<StorageQueueMessagingTransport>.GetNativeHeaderSize(
        IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return Base64.GetMaxEncodedToUtf8Length(
            StorageQueueEnvelopeCodec._measureCanonical(headers, ReadOnlySequence<byte>.Empty));
    }

    /// <summary>Measures the encoded native header framing for an empty payload.</summary>
    /// <param name="headers">The envelope headers.</param>
    /// <returns>The encoded native header framing size in bytes.</returns>
    public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
    {
        var canonicalBytes = StorageQueueEnvelopeCodec._measureCanonical(headers, ReadOnlySequence<byte>.Empty);
        return Base64.GetMaxEncodedToUtf8Length(canonicalBytes);
    }

    /// <inheritdoc />
    public long GetMaximumInlinePayloadBytes(IReadOnlyDictionary<string, string> headers)
    {
        return Math.Max(
            0,
            StorageQueueLimits.MaximumNormalCanonicalBytes
                - StorageQueueEnvelopeCodec._measureCanonical(headers, ReadOnlySequence<byte>.Empty));
    }

    /// <inheritdoc />
    public long MeasureNativePayload(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload)
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
        queue = ToNativeEntityName(queue);
        ArgumentNullException.ThrowIfNull(headers);

        var encoded = StorageQueueEnvelopeCodec.Encode(headers, payload);
        if (Encoding.UTF8.GetByteCount(encoded) > MaximumPayloadSizeBytes)
            throw new ArgumentOutOfRangeException(
                nameof(payload),
                "The completed Storage Queue message exceeds the conservative 48 KiB limit.");
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

    /// <summary>Gets the maximum number of messages a single receive returns.</summary>
    /// <remarks>
    /// The service maximum for <c>ReceiveMessages</c>. Every receive is one billed transaction, so a
    /// full batch costs the same as a single message.
    /// </remarks>
    public const int MaximumReceiveBatchSize = 32;

    /// <summary>Computes the visibility timeout a processor needs for a handler duration.</summary>
    /// <param name="maximumHandlerDuration">The participant's maximum handler duration.</param>
    /// <param name="options">The processing options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The handler duration plus the expected buffer wait plus the renewal safety margin.</returns>
    /// <remarks>
    /// Storage Queues has no lock duration of its own: the visibility timeout <em>is</em> the lock, and
    /// it is a client-side receive parameter rather than a queue setting. A delivery may wait in the
    /// prefetch buffer before a worker reaches it, so the window must cover that wait as well as the
    /// handler, with the renewal margin on top so the shared renewer has time to extend it.
    /// </remarks>
    /// <exception cref="MessagingCompositionException">The derived window exceeds the seven-day service maximum.</exception>
    public static TimeSpan DeriveReceiveVisibilityTimeout(
        TimeSpan maximumHandlerDuration,
        MessagingProcessingOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumHandlerDuration, TimeSpan.Zero);

        options ??= new MessagingProcessingOptions();
        var concurrency = options.InitialConcurrency;
        var queued = Math.Max(0, options.ComputePrefetchBudget(concurrency, _capabilities(TimeSpan.FromMinutes(1))) - concurrency);
        var bufferWait = options.ExpectedHandlerDuration * (queued / (double)concurrency);
        var derived = maximumHandlerDuration + bufferWait + options.RenewalSafetyMargin;
        if (derived > _maximumVisibilityDelay)
        {
            throw new MessagingCompositionException(
                MessagingCompositionDiagnostic.ProcessingOptionsInvalid,
                FormattableString.Invariant(
                    $"The derived Storage Queue visibility timeout ({derived}) exceeds the seven-day service maximum. Lower MaximumHandlerDuration ({maximumHandlerDuration}) or the prefetch budget."));
        }

        return derived;
    }

    private static MessagingReceiverCapabilities _capabilities(TimeSpan visibilityTimeout)
    {
        return new MessagingReceiverCapabilities(
            MaximumBatchSize: MaximumReceiveBatchSize,
            SupportsServerSideWait: false,
            SupportsLockRenewal: true,
            NativeLockDuration: visibilityTimeout);
    }

    /// <inheritdoc />
    public MessagingReceiverCapabilities ReceiverCapabilities => _capabilities(_receiveVisibilityTimeout);

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

        queue = ToNativeEntityName(queue);
        var source = _queue(queue);
        var poison = _queue(_poisonQueue(queue));
        var requested = Math.Min(maxMessages, MaximumReceiveBatchSize);

        // Storage Queues has no server-side wait, so an empty queue returns immediately and the
        // host owns the backoff. Poison moves are retried within the same call so a single bad
        // message cannot make a non-empty queue look empty forever.
        while (true)
        {
            ctk.ThrowIfCancellationRequested();
            var response = await source.ReceiveMessagesAsync(
                maxMessages: requested,
                visibilityTimeout: _receiveVisibilityTimeout,
                cancellationToken: ctk).ConfigureAwait(false);
            if (response.Value.Length == 0)
                return Array.Empty<IMessagingLockedDelivery>();

            var batch = new List<IMessagingLockedDelivery>(response.Value.Length);
            foreach (var message in response.Value)
            {
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
                        message.PopReceipt,
                        exception.Reason.ToString(),
                        exception.Message,
                        ctk).ConfigureAwait(false);
                    continue;
                }

                batch.Add(new StorageQueueLockedDelivery(
                    source,
                    poison,
                    message,
                    envelope,
                    _receiveVisibilityTimeout,
                    _retryDelay));
            }

            if (batch.Count > 0)
                return batch;
        }
    }

    /// <inheritdoc />
    public async Task EnsureQueueAsync(
        string queue,
        int maximumDeliveryCount,
        string ownerIdentity,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        queue = ToNativeEntityName(queue);
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
        string popReceipt,
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
        await source.DeleteMessageAsync(message.MessageId, popReceipt, ctk)
            .ConfigureAwait(false);
    }

    private sealed class StorageQueueLockedDelivery : IMessagingLockedDelivery, IDisposable
    {
        private readonly QueueClient _source;
        private readonly QueueClient _poison;
        private readonly QueueMessage _message;
        private readonly TimeSpan _receiveVisibilityTimeout;
        private readonly TimeSpan _retryDelay;

        // Storage Queues rotates the pop receipt on every UpdateMessage, so the receipt is mutable
        // state shared by the renewer and the settling worker. The gate makes renewal and settlement
        // mutually exclusive for one delivery, which is what keeps settlement on the newest receipt.
        private readonly SemaphoreSlim _receipt = new(1, 1);
        private string _popReceipt;
        private long _lockedUntilTicks;

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
            _setLockedUntil(message.NextVisibleOn);
            Headers = envelope.Headers;
            Payload = envelope.Payload;
        }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public ReadOnlySequence<byte> Payload { get; }

        public int DeliveryCount => checked((int)_message.DequeueCount);

        public string DeliveryId => _message.MessageId;

        public DateTimeOffset? LockedUntil
        {
            get
            {
                var ticks = Interlocked.Read(ref _lockedUntilTicks);
                return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }

        public async Task RenewLockAsync(CancellationToken ctk)
        {
            // A renewal that loses the race to a settlement is pointless: settlement consumes the
            // lock, so skipping is correct and keeps the renewer's timer from blocking.
            if (!await _receipt.WaitAsync(0, ctk).ConfigureAwait(false))
                return;

            try
            {
                await _updateAsync(_receiveVisibilityTimeout, ctk).ConfigureAwait(false);
            }
            finally
            {
                _receipt.Release();
            }
        }

        public async Task CompleteAsync(CancellationToken ctk)
        {
            await _receipt.WaitAsync(ctk).ConfigureAwait(false);
            try
            {
                try
                {
                    await _source.DeleteMessageAsync(_message.MessageId, _popReceipt, ctk)
                        .ConfigureAwait(false);
                }
                catch (RequestFailedException exception) when (_isLockLost(exception))
                {
                    throw MessagingLockLostException._forDelivery(_message.MessageId, exception);
                }
            }
            finally
            {
                _receipt.Release();
            }
        }

        public async Task AbandonAsync(CancellationToken ctk)
        {
            await _receipt.WaitAsync(ctk).ConfigureAwait(false);
            try
            {
                await _updateAsync(_retryDelay, ctk).ConfigureAwait(false);
            }
            finally
            {
                _receipt.Release();
            }
        }

        public async Task DeadLetterAsync(
            string reason,
            string description,
            CancellationToken ctk)
        {
            await _receipt.WaitAsync(ctk).ConfigureAwait(false);
            try
            {
                try
                {
                    await _moveToPoisonAsync(
                        _source,
                        _poison,
                        _message,
                        _popReceipt,
                        reason,
                        description,
                        ctk).ConfigureAwait(false);
                }
                catch (RequestFailedException exception) when (_isLockLost(exception))
                {
                    throw MessagingLockLostException._forDelivery(_message.MessageId, exception);
                }
            }
            finally
            {
                _receipt.Release();
            }
        }

        public void Dispose()
        {
            _receipt.Dispose();
        }

        private async Task _updateAsync(TimeSpan visibilityTimeout, CancellationToken ctk)
        {
            try
            {
                var response = await _source.UpdateMessageAsync(
                    _message.MessageId,
                    _popReceipt,
                    _message.Body,
                    visibilityTimeout,
                    ctk).ConfigureAwait(false);
                _popReceipt = response.Value.PopReceipt;
                _setLockedUntil(response.Value.NextVisibleOn);
            }
            catch (RequestFailedException exception) when (_isLockLost(exception))
            {
                throw MessagingLockLostException._forDelivery(_message.MessageId, exception);
            }
        }

        private void _setLockedUntil(DateTimeOffset? lockedUntil)
        {
            Interlocked.Exchange(
                ref _lockedUntilTicks,
                lockedUntil?.ToUniversalTime().Ticks ?? 0);
        }

        private static bool _isLockLost(RequestFailedException exception)
        {
            // A rotated receipt makes the previous one invalid, and an expired visibility window
            // makes the message somebody else's: both are "the lock is gone", not a broken transport.
            return string.Equals(exception.ErrorCode, "MessageNotFound", StringComparison.Ordinal)
                || string.Equals(exception.ErrorCode, "PopReceiptMismatch", StringComparison.Ordinal)
                || exception.Status is 404;
        }
    }
}
