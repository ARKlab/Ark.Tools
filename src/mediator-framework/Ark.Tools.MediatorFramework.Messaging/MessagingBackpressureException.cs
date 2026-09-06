// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Thrown by a handler whose own downstream dependency is the throughput limit.</summary>
/// <remarks>
/// Throw this when the dependency's capacity is <em>known</em> — a connection pool size, a documented
/// rate limit, an HTTP 429 — because no host-side metric can infer it reliably. The delivery is
/// abandoned for retry rather than treated as a failure, and the host halves its concurrency limit.
/// </remarks>
public sealed class MessagingBackpressureException : Exception
{
    /// <summary>Creates a backpressure signal.</summary>
    public MessagingBackpressureException()
        : this("The handler's downstream dependency is saturated.", null, null)
    {
    }

    /// <summary>Creates a backpressure signal with a message.</summary>
    /// <param name="message">The reason the downstream dependency is saturated.</param>
    public MessagingBackpressureException(string message)
        : this(message, null, null)
    {
    }

    /// <summary>Creates a backpressure signal with a message and an inner exception.</summary>
    /// <param name="message">The reason the downstream dependency is saturated.</param>
    /// <param name="innerException">The dependency failure that triggered the signal.</param>
    public MessagingBackpressureException(string message, Exception? innerException)
        : this(message, innerException, null)
    {
    }

    /// <summary>Creates a backpressure signal with a requested redelivery delay.</summary>
    /// <param name="message">The reason the downstream dependency is saturated.</param>
    /// <param name="innerException">The dependency failure that triggered the signal, if any.</param>
    /// <param name="retryDelay">The delay the handler asks for before the message is retried.</param>
    public MessagingBackpressureException(string message, Exception? innerException, TimeSpan? retryDelay)
        : base(message, innerException)
    {
        RetryDelay = retryDelay;
    }

    /// <summary>Gets the redelivery delay requested by the handler, when it named one.</summary>
    /// <remarks>
    /// ponytail: the delay is advisory. Ceiling: no transport in this framework exposes a per-settlement
    /// visibility delay yet, so redelivery timing is the queue's own; the effective backpressure is the
    /// halved concurrency limit. Upgrade path: a deferred abandon on the transport seam.
    /// </remarks>
    public TimeSpan? RetryDelay { get; }
}
