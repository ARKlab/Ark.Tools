// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>One persisted envelope of an outbox batch send.</summary>
/// <param name="Headers">The envelope headers, without the outbox routing headers.</param>
/// <param name="Payload">The serialized payload.</param>
/// <param name="DueTime">The scheduled delivery time, only for queue destinations.</param>
public readonly record struct MessagingBatchEntry(
    IReadOnlyDictionary<string, string> Headers,
    ReadOnlySequence<byte> Payload,
    DateTimeOffset? DueTime);

/// <summary>
/// A transport that sends several envelopes to one destination with as few broker calls as it can.
/// The native outbox processor uses it when the transport implements it, and single sends otherwise.
/// </summary>
public interface IMessagingBatchTransport : IMessagingTransport
{
    /// <summary>Sends the envelopes to one queue or topic, in order.</summary>
    /// <param name="destination">The destination queue or topic.</param>
    /// <param name="isTopic">Whether <paramref name="destination"/> is a topic.</param>
    /// <param name="messages">The envelopes, in the order they must be accepted.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>
    /// One duration per envelope, in input order: the duration of the broker call that carried it.
    /// The task completes only when every envelope is accepted.
    /// </returns>
    Task<IReadOnlyList<TimeSpan>> SendBatchAsync(
        string destination,
        bool isTopic,
        IReadOnlyList<MessagingBatchEntry> messages,
        CancellationToken ctk);
}
