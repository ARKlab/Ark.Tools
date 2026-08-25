// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

using NodaTime;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>First-class in-memory transport with scheduled delivery and PeekLock settlement.</summary>
public sealed class InMemoryMessagingTransport : IMessagingReceiveTransport, IMessagingTransportManagement
{
    private const int _maximumDeadLetterReasonLength = 256;
    private const int _maximumDeadLetterDescriptionLength = 1_024;
    private const string _maximumDeliveryReason = "maximum-delivery-count";
    private const string _maximumDeliveryDescription = "The transport maximum delivery count was reached.";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, InMemoryQueue> _queues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _subscriptions = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly Duration _lockDuration;
    private long _scheduleSequence;

    /// <summary>Creates an in-memory transport using the system clock and one-minute locks.</summary>
    public InMemoryMessagingTransport()
        : this(SystemClock.Instance, Duration.FromMinutes(1))
    {
    }

    /// <summary>Creates an in-memory transport with a lock duration and the system clock.</summary>
    /// <param name="lockDuration">The PeekLock duration.</param>
    public InMemoryMessagingTransport(Duration lockDuration)
        : this(SystemClock.Instance, lockDuration)
    {
    }

    /// <summary>Creates an in-memory transport with a supplied clock and lock duration.</summary>
    /// <param name="clock">The clock used for scheduled delivery and lock expiry.</param>
    /// <param name="lockDuration">The PeekLock duration.</param>
    public InMemoryMessagingTransport(IClock clock, Duration lockDuration)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (lockDuration <= Duration.Zero)
            throw new ArgumentOutOfRangeException(nameof(lockDuration), "The lock duration must be positive.");

        _clock = clock;
        _lockDuration = lockDuration;
    }

    /// <inheritdoc />
    public MessagingCapabilities Capabilities =>
        MessagingCapabilities.Receive | MessagingCapabilities.PubSub | MessagingCapabilities.ScheduledSend;

    /// <inheritdoc />
    public long? MaximumInlineEnvelopeBytes => null;

    /// <summary>Configures the native retry limit and delay for a queue.</summary>
    /// <param name="queue">The queue name.</param>
    /// <param name="maximumDeliveryCount">The maximum native delivery count.</param>
    /// <param name="retryDelay">The delay before an abandoned delivery becomes visible.</param>
    public void ConfigureRetry(string queue, int maximumDeliveryCount, TimeSpan retryDelay)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDeliveryCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(retryDelay.Ticks, TimeSpan.Zero.Ticks, nameof(retryDelay));

        lock (_gate)
        {
            var target = _getOrAddQueue(queue);
            target._maximumDeliveryCount = maximumDeliveryCount;
            target._retryDelay = retryDelay;
        }
    }

    /// <inheritdoc />
    public long MeasureNative(IReadOnlyDictionary<string, string> headers, in ReadOnlySequence<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var total = payload.Length;
        checked
        {
            foreach (var pair in headers)
                total += System.Text.Encoding.UTF8.GetByteCount(pair.Key)
                    + System.Text.Encoding.UTF8.GetByteCount(pair.Value);
        }

        return total;
    }

    /// <inheritdoc />
    public Task SendAsync(
        string queue,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        DateTimeOffset? dueTime,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ArgumentNullException.ThrowIfNull(headers);
        ctk.ThrowIfCancellationRequested();

        var envelope = InMemoryEnvelope._create(headers, payload);
        lock (_gate)
        {
            var target = _getOrAddQueue(queue);
            if (dueTime is { } due)
            {
                target._scheduled.Enqueue(
                    envelope,
                    (Instant.FromDateTimeOffset(due), _scheduleSequence++));
            }
            else
            {
                target._visible.Enqueue(envelope);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PublishAsync(
        string topic,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentNullException.ThrowIfNull(headers);
        ctk.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_subscriptions.TryGetValue(topic, out var subscriptions))
            {
                foreach (var queue in subscriptions.Values)
                    _getOrAddQueue(queue)._visible.Enqueue(InMemoryEnvelope._create(headers, payload));
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<IMessagingLockedDelivery> ReceiveAsync(
        string queue,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        return _receiveAsync(queue, ctk);
    }

    private async IAsyncEnumerable<IMessagingLockedDelivery> _receiveAsync(
        string queue,
        [EnumeratorCancellation] CancellationToken ctk)
    {
        while (true)
        {
            ctk.ThrowIfCancellationRequested();
            InMemoryLockedDelivery? delivery = null;
            lock (_gate)
            {
                var target = _getOrAddQueue(queue);
                var now = _clock.GetCurrentInstant();
                target._promoteDue(now);
                target._expireLocks(now);
                if (target._visible.TryDequeue(out var envelope))
                {
                    envelope._deliveryCount++;
                    var lockId = Guid.NewGuid();
                    target._locked.Add(lockId, new InMemoryLock(envelope, now + _lockDuration));
                    delivery = new InMemoryLockedDelivery(this, target, lockId, envelope);
                }
            }

            if (delivery is not null)
            {
                yield return delivery;
                continue;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), ctk).ConfigureAwait(false);
        }
    }

    /// <summary>Gets a snapshot of dead letters collected for a queue.</summary>
    /// <param name="queue">The queue name.</param>
    /// <returns>The dead-letter entries in settlement order.</returns>
    public IReadOnlyList<InMemoryDeadLetter> GetDeadLetters(string queue)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        lock (_gate)
        {
            return _queues.TryGetValue(queue, out var target)
                ? new ReadOnlyCollection<InMemoryDeadLetter>(target._deadLetters.ToArray())
                : Array.Empty<InMemoryDeadLetter>();
        }
    }

    /// <inheritdoc />
    public Task EnsureQueueAsync(string queue, CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(queue);
        ctk.ThrowIfCancellationRequested();
        lock (_gate)
            _getOrAddQueue(queue);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureTopicAsync(string topic, CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ctk.ThrowIfCancellationRequested();
        lock (_gate)
            _subscriptions.TryAdd(topic, new Dictionary<string, string>(StringComparer.Ordinal));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureSubscriptionAsync(
        string topic,
        string subscription,
        string forwardToQueue,
        CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(subscription);
        ArgumentException.ThrowIfNullOrEmpty(forwardToQueue);
        ctk.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var subscriptions = _subscriptions.TryGetValue(topic, out var existing)
                ? existing
                : (_subscriptions[topic] = new Dictionary<string, string>(StringComparer.Ordinal));
            subscriptions[subscription] = forwardToQueue;
            _getOrAddQueue(forwardToQueue);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteSubscriptionAsync(string topic, string subscription, CancellationToken ctk)
    {
        ArgumentException.ThrowIfNullOrEmpty(topic);
        ArgumentException.ThrowIfNullOrEmpty(subscription);
        ctk.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(topic, out var subscriptions))
                subscriptions.Remove(subscription);
        }

        return Task.CompletedTask;
    }

    private InMemoryQueue _getOrAddQueue(string name)
    {
        if (_queues.TryGetValue(name, out var queue))
            return queue;

        queue = new InMemoryQueue();
        _queues.Add(name, queue);
        return queue;
    }

    private Task _settle(
        InMemoryQueue queue,
        Guid lockId,
        Settlement settlement,
        string? reason,
        string? description,
        CancellationToken ctk)
    {
        ctk.ThrowIfCancellationRequested();
        lock (_gate)
        {
            queue._expireLocks(_clock.GetCurrentInstant());
            if (!queue._locked.Remove(lockId, out var locked))
                throw new InvalidOperationException("The messaging delivery has already been settled or expired.");

            if (settlement == Settlement.Abandon
                && locked._envelope._deliveryCount >= queue._maximumDeliveryCount)
            {
                queue._deadLetters.Add(new InMemoryDeadLetter(
                    locked._envelope._headers,
                    locked._envelope._payload,
                    locked._envelope._deliveryCount,
                    _maximumDeliveryReason,
                    _maximumDeliveryDescription));
            }
            else if (settlement == Settlement.Abandon)
            {
                var due = _clock.GetCurrentInstant()
                    + Duration.FromTimeSpan(queue._retryDelay);
                if (due <= _clock.GetCurrentInstant())
                    queue._visible.Enqueue(locked._envelope);
                else
                    queue._scheduled.Enqueue(
                        locked._envelope,
                        (due, _scheduleSequence++));
            }
            else if (settlement == Settlement.DeadLetter)
            {
                queue._deadLetters.Add(new InMemoryDeadLetter(
                    locked._envelope._headers,
                    locked._envelope._payload,
                    locked._envelope._deliveryCount,
                    reason!,
                    description!));
            }
        }

        return Task.CompletedTask;
    }

    private Task _renew(InMemoryQueue queue, Guid lockId, CancellationToken ctk)
    {
        ctk.ThrowIfCancellationRequested();
        lock (_gate)
        {
            queue._expireLocks(_clock.GetCurrentInstant());
            if (!queue._locked.TryGetValue(lockId, out var locked))
                throw new InvalidOperationException("The messaging delivery has already been settled or expired.");
            locked._lockedUntil = _clock.GetCurrentInstant() + _lockDuration;
        }

        return Task.CompletedTask;
    }

    private sealed class InMemoryQueue
    {
        internal Queue<InMemoryEnvelope> _visible { get; } = new();
        internal PriorityQueue<InMemoryEnvelope, (Instant Due, long Sequence)> _scheduled { get; } = new();
        internal Dictionary<Guid, InMemoryLock> _locked { get; } = [];
        internal List<InMemoryDeadLetter> _deadLetters { get; } = [];
        internal int _maximumDeliveryCount { get; set; } = int.MaxValue;
        internal TimeSpan _retryDelay { get; set; }

        internal void _promoteDue(Instant now)
        {
            while (_scheduled.TryPeek(out _, out var priority) && priority.Due <= now)
                _visible.Enqueue(_scheduled.Dequeue());
        }

        internal void _expireLocks(Instant now)
        {
            foreach (var pair in _locked.ToArray())
            {
                if (pair.Value._lockedUntil > now)
                    continue;

                _locked.Remove(pair.Key);
                if (pair.Value._envelope._deliveryCount >= _maximumDeliveryCount)
                {
                    _deadLetters.Add(new InMemoryDeadLetter(
                        pair.Value._envelope._headers,
                        pair.Value._envelope._payload,
                        pair.Value._envelope._deliveryCount,
                        _maximumDeliveryReason,
                        _maximumDeliveryDescription));
                }
                else
                {
                    _visible.Enqueue(pair.Value._envelope);
                }
            }
        }

    }

    private sealed class InMemoryEnvelope
    {
        private InMemoryEnvelope(IReadOnlyDictionary<string, string> headers, ReadOnlySequence<byte> payload)
        {
            _headers = headers;
            _payload = payload;
        }

        internal IReadOnlyDictionary<string, string> _headers { get; }
        internal ReadOnlySequence<byte> _payload { get; }
        internal int _deliveryCount { get; set; }

        internal static InMemoryEnvelope _create(
            IReadOnlyDictionary<string, string> headers,
            in ReadOnlySequence<byte> payload)
        {
            var headerCopy = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(headers, StringComparer.Ordinal));
            return new InMemoryEnvelope(headerCopy, new ReadOnlySequence<byte>(payload.ToArray()));
        }
    }

    private sealed class InMemoryLock
    {
        internal InMemoryLock(InMemoryEnvelope envelope, Instant lockedUntil)
        {
            _envelope = envelope;
            _lockedUntil = lockedUntil;
        }

        internal InMemoryEnvelope _envelope { get; }
        internal Instant _lockedUntil { get; set; }
    }

    private enum Settlement
    {
        Complete,
        Abandon,
        DeadLetter
    }

    private sealed class InMemoryLockedDelivery : IMessagingLockedDelivery
    {
        private readonly InMemoryMessagingTransport _transport;
        private readonly InMemoryQueue _queue;
        private readonly Guid _lockId;
        private readonly InMemoryEnvelope _envelope;

        internal InMemoryLockedDelivery(
            InMemoryMessagingTransport transport,
            InMemoryQueue queue,
            Guid lockId,
            InMemoryEnvelope envelope)
        {
            _transport = transport;
            _queue = queue;
            _lockId = lockId;
            _envelope = envelope;
        }

        public IReadOnlyDictionary<string, string> Headers => _envelope._headers;

        public ReadOnlySequence<byte> Payload => _envelope._payload;

        public int DeliveryCount => _envelope._deliveryCount;

        public Task CompleteAsync(CancellationToken ctk)
        {
            return _transport._settle(_queue, _lockId, Settlement.Complete, null, null, ctk);
        }

        public Task RenewLockAsync(CancellationToken ctk)
        {
            return _transport._renew(_queue, _lockId, ctk);
        }

        public Task AbandonAsync(CancellationToken ctk)
        {
            return _transport._settle(_queue, _lockId, Settlement.Abandon, null, null, ctk);
        }

        public Task DeadLetterAsync(string reason, string description, CancellationToken ctk)
        {
            ArgumentException.ThrowIfNullOrEmpty(reason);
            ArgumentNullException.ThrowIfNull(description);
            if (reason.Length > _maximumDeadLetterReasonLength)
                throw new ArgumentException("The dead-letter reason is too long.", nameof(reason));
            if (description.Length > _maximumDeadLetterDescriptionLength)
                throw new ArgumentException("The dead-letter description is too long.", nameof(description));

            return _transport._settle(_queue, _lockId, Settlement.DeadLetter, reason, description, ctk);
        }

    }
}

/// <summary>A dead-letter entry collected by <see cref="InMemoryMessagingTransport"/>.</summary>
public sealed class InMemoryDeadLetter
{
    internal InMemoryDeadLetter(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        int deliveryCount,
        string reason,
        string description)
    {
        Headers = headers;
        Payload = payload;
        DeliveryCount = deliveryCount;
        Reason = reason;
        Description = description;
    }

    /// <summary>Gets the dead-letter headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>Gets the dead-letter payload.</summary>
    public ReadOnlySequence<byte> Payload { get; }

    /// <summary>Gets the delivery count at dead-letter settlement.</summary>
    public int DeliveryCount { get; }

    /// <summary>Gets the dead-letter reason.</summary>
    public string Reason { get; }

    /// <summary>Gets the dead-letter description.</summary>
    public string Description { get; }
}
