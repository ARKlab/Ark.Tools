// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;
using Ark.Tools.Outbox;

namespace Ark.MediatorFramework.Sample.Core.Functions.Hosting;

/// <summary>
/// The Functions variant's bus: the handlers still enlist their data context's outbox, but nothing drains it in a
/// Functions-only deployment, so enlisting is a no-op and every message goes straight to the transport.
/// </summary>
/// <remarks>
/// There is no outbox: a message is sent when the handler sends it, before the data context commits. If the handler or
/// the commit then fails, the message has been sent for a change that was not stored.
/// </remarks>
/// <param name="inner">The native messaging bus.</param>
public sealed class DirectSendBus(IBus inner) : IBus, IBusOutboxEnlistment
{
    private static readonly NoOutboxScope _noOutboxScope = new();

    /// <inheritdoc />
    public IBusOutboxScope Enlist(IOutboxContextCore context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _noOutboxScope;
    }

    /// <inheritdoc />
    public async Task Send<T>(
        T message,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await inner.Send(message, additionalHeaders, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Defer<T>(
        T message,
        TimeSpan delay,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await inner.Defer(message, delay, additionalHeaders, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Defer<T>(
        T message,
        DateTimeOffset dueTime,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await inner.Defer(message, dueTime, additionalHeaders, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Publish<T>(
        T @event,
        IReadOnlyDictionary<string, string>? additionalHeaders = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        await inner.Publish(@event, additionalHeaders, cancellationToken).ConfigureAwait(false);
    }

    private sealed class NoOutboxScope : IBusOutboxScope
    {
        public async Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public void Dispose()
        {
        }
    }
}
