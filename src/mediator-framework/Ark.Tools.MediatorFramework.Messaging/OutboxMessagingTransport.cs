// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using Ark.Tools.Outbox;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>
/// Stages sends and publishes in the ambient <see cref="OutboxScope"/>, with the routing headers the
/// <see cref="MessagingOutboxProcessor"/> needs, and forwards them to the inner transport otherwise.
/// </summary>
internal sealed class OutboxMessagingTransport : IMessagingTransport
{
    private readonly IMessagingTransport _inner;

    public OutboxMessagingTransport(IMessagingTransport inner)
    {
        _inner = inner;
    }

    public MessagingCapabilities Capabilities => _inner.Capabilities;

    public long MaximumPayloadBytes => _inner.MaximumPayloadBytes;

    public long GetMaximumInlinePayloadBytes(IReadOnlyDictionary<string, string> headers)
    {
        return _inner.GetMaximumInlinePayloadBytes(headers);
    }

    public long MeasureNativeHeaders(IReadOnlyDictionary<string, string> headers)
    {
        return _inner.MeasureNativeHeaders(headers);
    }

    public long MeasureNativePayload(IReadOnlyDictionary<string, string> headers, ReadOnlySequence<byte> payload)
    {
        return _inner.MeasureNativePayload(headers, payload);
    }

    public async Task SendAsync(
        string queue,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        DateTimeOffset? dueTime,
        CancellationToken ctk)
    {
        if (!_tryStage("queue", queue, headers, payload, dueTime))
            await _inner.SendAsync(queue, headers, payload, dueTime, ctk).ConfigureAwait(false);
    }

    public async Task PublishAsync(
        string topic,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        CancellationToken ctk)
    {
        if (!_tryStage("topic", topic, headers, payload, dueTime: null))
            await _inner.PublishAsync(topic, headers, payload, ctk).ConfigureAwait(false);
    }

    private static bool _tryStage(
        string destinationKind,
        string destination,
        IReadOnlyDictionary<string, string> headers,
        ReadOnlySequence<byte> payload,
        DateTimeOffset? dueTime)
    {
        var scope = OutboxScope.Current;
        if (scope is null)
            return false;

        // Sized for the copied headers plus the up to three outbox keys added below.
        var outboxHeaders = new Dictionary<string, string>(headers.Count + 3, StringComparer.Ordinal);
        foreach (var pair in headers)
            outboxHeaders.Add(pair.Key, pair.Value);
        outboxHeaders[MessagingHeaders.OutboxDestinationKind] = destinationKind;
        outboxHeaders[MessagingHeaders.OutboxDestination] = destination;
        if (dueTime is not null)
            outboxHeaders[MessagingHeaders.OutboxDueTime] = dueTime.Value.ToString("O", CultureInfo.InvariantCulture);
        MessagingBus._validateHeaders(outboxHeaders);
        scope.Add(new OutboxMessage
        {
            Headers = outboxHeaders,
            Body = payload.ToArray(),
        });
        return true;
    }
}
