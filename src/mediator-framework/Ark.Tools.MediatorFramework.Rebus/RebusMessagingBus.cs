// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using RebusBus = Rebus.Bus.IBus;

using System.Collections.Frozen;

namespace Ark.Tools.MediatorFramework.Rebus;

/// <summary>Adapts a Rebus bus to the transport-neutral one-way messaging API.</summary>
public sealed class RebusMessagingBus : IBus
{
    private const string _senderIdentityHeader = "ark-sender-identity";
    private readonly RebusBus _bus;
    private readonly string _senderIdentity;
    private readonly FrozenSet<Type> _publishedTypes;
    private readonly bool _outboxEnabled;

    /// <summary>Creates a transport-neutral adapter over Rebus.</summary>
    /// <param name="bus">The Rebus bus.</param>
    /// <param name="senderIdentity">The identity of the sending participant.</param>
    /// <param name="publishedTypes">The event types the participant publishes.</param>
    /// <param name="outboxEnabled">
    /// Whether the Rebus transport is configured with the Ark outbox (<c>transport.Outbox(...)</c>), which stages the
    /// sends made in an open <see cref="OutboxScope"/>. Without it, sending in an open scope throws, because the message
    /// would not be stored atomically with the application change.
    /// </param>
    public RebusMessagingBus(RebusBus bus, string senderIdentity, IEnumerable<Type> publishedTypes, bool outboxEnabled)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        ArgumentException.ThrowIfNullOrEmpty(senderIdentity);
        ArgumentNullException.ThrowIfNull(publishedTypes);
        _senderIdentity = senderIdentity;
        _publishedTypes = publishedTypes.ToFrozenSet();
        _outboxEnabled = outboxEnabled;
    }

    /// <inheritdoc />
    public async Task Send<T>(
        T message,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        _throwIfScopeWithoutOutbox();
        await _bus.Send(message, _headers(additionalHeaders)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public async Task Defer<T>(
        T message,
        TimeSpan delay,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();
        _throwIfScopeWithoutOutbox();
        await _bus.Defer(delay, message, _headers(additionalHeaders)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public async Task Defer<T>(
        T message,
        DateTimeOffset dueTime,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var delay = dueTime - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(dueTime), "The due time must not be in the past.");
        await Defer(message, delay, additionalHeaders, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Publish<T>(
        T @event,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (!_publishedTypes.Contains(typeof(T)))
        {
            throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Participant '{0}' does not declare event '{1}' in Publishes.",
                    _senderIdentity,
                    typeof(T).FullName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        _throwIfScopeWithoutOutbox();
        await _bus.Publish(@event, _headers(additionalHeaders)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void _throwIfScopeWithoutOutbox()
    {
        if (!_outboxEnabled && OutboxScope.Current is not null)
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Rebus participant '{0}' has no outbox, so a message sent in an OutboxScope would not be stored with the application change. Configure the Rebus transport with Outbox(...) and create RebusMessagingBus with outboxEnabled: true.",
                    _senderIdentity));
    }

    private Dictionary<string, string> _headers(IReadOnlyDictionary<string, string>? additionalHeaders)
    {
        var headers = additionalHeaders is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(additionalHeaders, StringComparer.Ordinal);
        if (headers.ContainsKey(_senderIdentityHeader))
            throw new ArgumentException("The Rebus sender identity header is reserved.", nameof(additionalHeaders));
        headers[_senderIdentityHeader] = _senderIdentity;
        return headers;
    }
}
