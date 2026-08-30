// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;
using Ark.Tools.Outbox.Rebus;

using RebusBus = Rebus.Bus.IBus;

using System.Collections.Frozen;

namespace Ark.Tools.MediatorFramework.Rebus;

/// <summary>Adapts a Rebus bus to the transport-neutral one-way messaging API.</summary>
public sealed class RebusMessagingBus : IBus, IBusOutboxEnlistment
{
    private const string _senderIdentityHeader = "ark-sender-identity";
    private readonly RebusBus _bus;
    private readonly string _senderIdentity;
    private readonly FrozenSet<Type> _publishedTypes;

    /// <summary>Creates a transport-neutral adapter over Rebus.</summary>
    public RebusMessagingBus(RebusBus bus, string senderIdentity, IEnumerable<Type> publishedTypes)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        ArgumentException.ThrowIfNullOrEmpty(senderIdentity);
        ArgumentNullException.ThrowIfNull(publishedTypes);
        _senderIdentity = senderIdentity;
        _publishedTypes = publishedTypes.ToFrozenSet();
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
        await _bus.Publish(@event, _headers(additionalHeaders)).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <inheritdoc />
    public IBusOutboxScope Enlist(IOutboxContextCore context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new RebusOutboxScope(_bus.Enlist(context));
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

    private sealed class RebusOutboxScope : IBusOutboxScope
    {
        private readonly global::Rebus.Transport.RebusTransactionScope _scope;

        public RebusOutboxScope(global::Rebus.Transport.RebusTransactionScope scope)
        {
            _scope = scope;
        }

        public async Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _scope.CompleteAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
