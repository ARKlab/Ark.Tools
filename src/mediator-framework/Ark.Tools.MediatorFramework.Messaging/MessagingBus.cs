// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics;

using Ark.Tools.Outbox;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Transport-neutral native implementation of the restricted one-way bus.</summary>
public sealed class MessagingBus : IBus, IBusOutboxEnlistment, IDisposable
{
    private const int _maximumHeaderCount = 32;
    private const int _maximumHeaderKeyBytes = 128;
    private const int _maximumHeaderValueBytes = 4096;

    private readonly IMessagingTransport _transport;
    private readonly MessagingNetworkOptions _network;
    private readonly IMessagingContractRegistry _registry;
    private readonly IMessagingCodecRegistry _codecs;
    private readonly MessagingPayloadSender _payloadSender;
    private readonly string _participantIdentity;
    private readonly IReadOnlyList<Type> _outgoingStepTypes;
    private readonly Func<Type, object> _resolveStep;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly AsyncLocal<MessagingOutboxScope?> _outboxScope = new();
    private int _disposed;

    /// <summary>Creates a native bus over a composed messaging transport.</summary>
    /// <param name="transport">The transport used for sends.</param>
    /// <param name="network">The resolved network options.</param>
    /// <param name="registry">The generated contract routing registry.</param>
    /// <param name="codecs">The installed serialization codecs.</param>
    /// <param name="payloadSender">The payload serialization and claim-check runtime.</param>
    /// <param name="participantIdentity">The identity of the sending participant.</param>
    /// <param name="outgoingStepTypes">Optional outgoing pipeline steps.</param>
    /// <param name="resolveStep">The resolver for outgoing pipeline steps.</param>
    /// <param name="utcNow">The clock used for message and scheduled-send timestamps.</param>
    public MessagingBus(
        IMessagingTransport transport,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        IMessagingCodecRegistry codecs,
        MessagingPayloadSender payloadSender,
        string participantIdentity,
        IReadOnlyList<Type>? outgoingStepTypes = null,
        Func<Type, object>? resolveStep = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _codecs = codecs ?? throw new ArgumentNullException(nameof(codecs));
        _payloadSender = payloadSender ?? throw new ArgumentNullException(nameof(payloadSender));
        ArgumentException.ThrowIfNullOrEmpty(participantIdentity);
        if (string.Equals(participantIdentity, MessagingOutboxProcessor.Identity, StringComparison.Ordinal))
            throw new ArgumentException(
                "The participant identity 'outbox-processor' is reserved.",
                nameof(participantIdentity));
        if (!string.Equals(network.NetworkIdentity, registry.NetworkIdentity, StringComparison.Ordinal))
            throw new ArgumentException("The registry and network identities must match.", nameof(registry));

        _network.Validate(transport.Capabilities);
        _participantIdentity = participantIdentity;
        _outgoingStepTypes = new ReadOnlyCollection<Type>(
            (outgoingStepTypes ?? Array.Empty<Type>()).ToArray());
        _resolveStep = resolveStep ?? (_ =>
            throw new InvalidOperationException("A pipeline step resolver is required when outgoing steps are configured."));
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task Send<T>(
        T message,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await _sendCoreAsync(message, dueTime: null, additionalHeaders, cancellationToken, "send").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Defer<T>(
        T message,
        TimeSpan delay,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        _requireScheduledSend();
        if (delay < TimeSpan.Zero || delay > _network.MaximumSchedulingDelay)
            throw new ArgumentOutOfRangeException(nameof(delay));

        var now = _utcNow();
        await _sendCoreAsync(message, now + delay, additionalHeaders, cancellationToken, "defer").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Defer<T>(
        T message,
        DateTimeOffset dueTime,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        _requireScheduledSend();
        var now = _utcNow();
        if (dueTime < now)
            throw new ArgumentOutOfRangeException(nameof(dueTime), "The due time cannot be in the past.");
        if (dueTime - now > _network.MaximumSchedulingDelay)
            throw new ArgumentOutOfRangeException(nameof(dueTime));

        await _sendCoreAsync(message, dueTime, additionalHeaders, cancellationToken, "defer").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Publish<T>(
        T @event,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        _throwIfDisposed();
        ArgumentNullException.ThrowIfNull(@event);
        _requireNetworkCapability(MessagingCapabilities.PubSub);

        var publisher = _registry.GetPublisherIdentity<T>();
        if (!string.Equals(publisher, _participantIdentity, StringComparison.Ordinal))
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Participant '{0}' does not publish contract '{1}'.",
                    _participantIdentity,
                    _registry.GetLogicalName<T>()));

        await _runOutgoingAsync(
            @event,
            _registry.GetDestination<T>(),
            additionalHeaders,
            publish: true,
            dueTime: null,
            cancellationToken: cancellationToken,
            operation: "publish").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IBusOutboxScope Enlist(IOutboxContextCore context)
    {
        _throwIfDisposed();
        ArgumentNullException.ThrowIfNull(context);
        var scope = new MessagingOutboxScope(this, context, _outboxScope.Value);
        _outboxScope.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }

    private async Task _sendCoreAsync<T>(
        T message,
        DateTimeOffset? dueTime,
        IReadOnlyDictionary<string, string>? additionalHeaders,
        CancellationToken cancellationToken,
        string operation)
        where T : class
    {
        _throwIfDisposed();
        ArgumentNullException.ThrowIfNull(message);
        _requireNetworkCapability(MessagingCapabilities.SendReceive);
        var queue = _registry.GetProcessorIdentity<T>();
        await _runOutgoingAsync(
            message,
            queue,
            additionalHeaders,
            publish: false,
            dueTime,
            cancellationToken: cancellationToken,
            operation: operation).ConfigureAwait(false);
    }

    private async Task _runOutgoingAsync<T>(
        T message,
        string destination,
        IReadOnlyDictionary<string, string>? additionalHeaders,
        bool publish,
        DateTimeOffset? dueTime,
        CancellationToken cancellationToken,
        string operation)
        where T : class
    {
        _throwIfDisposed();
        var headers = _createHeaders<T>(additionalHeaders);
        var context = new MessagingOutgoingContext(headers, destination);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await MessagingPipelineInvoker.InvokeOutgoingAsync(
                _outgoingStepTypes,
                _resolveStep,
                context,
                async () =>
                {
                    var codec = _codecs.GetByProtocol(_registry.GetWireProtocol<T>());
                    var payload = await _payloadSender
                        .BuildOutgoingPayloadAsync(message, codec, _transport, context.Headers, cancellationToken)
                        .ConfigureAwait(false);
                    _validateHeaders(context.Headers);
                    var transportHeaders = new ReadOnlyDictionary<string, string>(
                        new Dictionary<string, string>(context.Headers, StringComparer.Ordinal));
                    var outboxScope = _outboxScope.Value;
                    if (outboxScope is not null)
                    {
                        var outboxHeaders = new Dictionary<string, string>(context.Headers, StringComparer.Ordinal)
                        {
                            [MessagingHeaders.OutboxDestinationKind] = publish ? "topic" : "queue",
                            [MessagingHeaders.OutboxDestination] = destination,
                        };
                        if (dueTime is not null)
                            outboxHeaders[MessagingHeaders.OutboxDueTime] =
                                dueTime.Value.ToString("O", CultureInfo.InvariantCulture);
                        _validateHeaders(outboxHeaders);
                        outboxScope.Add(new OutboxMessage { Headers = outboxHeaders, Body = payload.ToArray() });
                    }
                    else if (publish)
                        await _transport.PublishAsync(destination, transportHeaders, payload, cancellationToken)
                            .ConfigureAwait(false);
                    else
                        await _transport.SendAsync(
                            destination, transportHeaders, payload, dueTime, cancellationToken).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            MessagingMetrics.RecordClientOperation(stopwatch.Elapsed, headers, operation, destination);
        }
    }

    private Dictionary<string, string> _createHeaders<T>(
        IReadOnlyDictionary<string, string>? additionalHeaders)
        where T : class
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MessagingHeaders.MessageType] = _registry.GetLogicalName<T>(),
            [MessagingHeaders.MessageId] = Guid.NewGuid().ToString("N"),
            [MessagingHeaders.SentTime] = _utcNow().ToString("O", CultureInfo.InvariantCulture),
            [MessagingHeaders.Network] = _network.NetworkIdentity,
            [MessagingHeaders.SenderIdentity] = _participantIdentity,
        };

        if (additionalHeaders is null)
            return headers;

        foreach (var pair in additionalHeaders)
        {
            if (string.IsNullOrEmpty(pair.Key))
                throw new ArgumentException("Header names cannot be empty.", nameof(additionalHeaders));
            if (pair.Value is null)
                throw new ArgumentException("Header values cannot be null.", nameof(additionalHeaders));
            if (MessagingHeadersGuard.IsReserved(pair.Key))
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Header '{0}' is reserved and cannot be overridden.",
                        pair.Key),
                    nameof(additionalHeaders));
            headers[pair.Key] = pair.Value;
        }

        _validateHeaders(headers);
        return headers;
    }

    private static void _validateHeaders(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var count = 0;
        foreach (var pair in headers)
        {
            count++;
            if (Encoding.UTF8.GetByteCount(pair.Key) > _maximumHeaderKeyBytes
                || Encoding.UTF8.GetByteCount(pair.Value) > _maximumHeaderValueBytes)
                throw new ArgumentException("A messaging header exceeds its size limit.", nameof(headers));
        }

        if (count > _maximumHeaderCount)
            throw new ArgumentException("The messaging header count exceeds its size limit.", nameof(headers));
    }

    private void _requireScheduledSend()
    {
        _throwIfDisposed();
        _requireNetworkCapability(MessagingCapabilities.ScheduledSend);
    }

    private void _requireNetworkCapability(MessagingCapabilities capability)
    {
        if ((_network.Requires & capability) != capability)
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Network '{0}' does not declare the '{1}' capability.",
                    _network.NetworkIdentity,
                    capability));
        if ((_transport.Capabilities & capability) != capability)
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Transport does not declare the '{0}' capability.",
                    capability));
    }

    private void _throwIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    private sealed class MessagingOutboxScope : IBusOutboxScope
    {
        private readonly MessagingBus _bus;
        private readonly IOutboxContextCore _context;
        private readonly MessagingOutboxScope? _parent;
        private readonly List<OutboxMessage> _messages = [];
        private bool _completed;
        private bool _disposed;

        public MessagingOutboxScope(
            MessagingBus bus,
            IOutboxContextCore context,
            MessagingOutboxScope? parent)
        {
            _bus = bus;
            _context = context;
            _parent = parent;
        }

        public void Add(OutboxMessage message)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
                throw new InvalidOperationException("The messaging outbox scope has already completed.");
            _messages.Add(message);
        }

        public async Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completed)
                throw new InvalidOperationException("The messaging outbox scope has already completed.");

            await _context.SendAsync(_messages, cancellationToken).ConfigureAwait(false);
            _completed = true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            if (ReferenceEquals(_bus._outboxScope.Value, this))
                _bus._outboxScope.Value = _parent;
            _disposed = true;
        }
    }
}
