// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Messages;

/// <summary>Acknowledgement returned by the dead-letter demonstration request (never produced: the handler always throws).</summary>
public sealed record DeadLetterAck;

/// <summary>
/// Pure request exposed over Rebus only (<see cref="RebusMessageAttribute"/>) whose handler always throws,
/// demonstrating the Rebus dead-letter behavior: after the delivery attempts are exhausted the message is
/// forwarded to the error queue with the exception serialized into its headers.
/// </summary>
[Message(OwnerQueue = "ark.mediator.sample")]
[RebusMessage(OwnerQueue = "ark.mediator.sample")]
public sealed record FailingRebusRequest : IRequest<FailingRebusRequest, DeadLetterAck>
{
    /// <summary>Gets the reason surfaced in the thrown exception.</summary>
    public string Reason { get; init; } = "boom";
}

/// <summary>Event raised when a book print process completes.</summary>
[Event(OwnerPublisher = "ark.mediator.sample")]
public sealed record BookPrintCompleted : ICommand<BookPrintCompleted>
{
    /// <summary>Gets the completed print-process identifier.</summary>
    public Guid ProcessId { get; init; }
}
