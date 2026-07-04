// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Acknowledgement returned by the dead-letter demonstration request (never produced: the handler always throws).</summary>
public sealed record DeadLetterAck;

/// <summary>
/// Pure request exposed over Rebus only (<see cref="RebusMessageAttribute"/>) whose handler always throws,
/// demonstrating the Rebus dead-letter behavior: after the delivery attempts are exhausted the message is
/// forwarded to the error queue with the exception serialized into its headers.
/// </summary>
[RebusMessage]
public sealed record FailingRebusRequest : IRequest<DeadLetterAck>
{
    /// <summary>Gets the reason surfaced in the thrown exception.</summary>
    public string Reason { get; init; } = "boom";
}

/// <summary>Pure handler for <see cref="FailingRebusRequest"/> that always throws to force dead-lettering.</summary>
public sealed class FailingRebusRequestHandler : IRequestHandler<FailingRebusRequest, DeadLetterAck>
{
    /// <inheritdoc />
    public Task<DeadLetterAck> ExecuteAsync(FailingRebusRequest Request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(Request);
        throw new InvalidOperationException(Request.Reason);
    }
}
