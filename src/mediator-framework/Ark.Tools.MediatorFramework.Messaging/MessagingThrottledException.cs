// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Thrown when the broker refused a call because the caller is being throttled.</summary>
/// <remarks>
/// Transports raise this instead of a provider-specific error so the processor host can feed the
/// concurrency controller a throttling signal rather than treating it as an unknown failure.
/// </remarks>
public sealed class MessagingThrottledException : InvalidOperationException
{
    /// <summary>Creates a throttling failure.</summary>
    public MessagingThrottledException()
        : base("The broker is throttling requests.")
    {
    }

    /// <summary>Creates a throttling failure with a message.</summary>
    /// <param name="message">The human-readable explanation.</param>
    public MessagingThrottledException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a throttling failure with a message and a cause.</summary>
    /// <param name="message">The human-readable explanation.</param>
    /// <param name="innerException">The cause.</param>
    public MessagingThrottledException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the native entity the broker throttled.</summary>
    public string Entity { get; internal init; } = string.Empty;

    internal static MessagingThrottledException _forEntity(string entity, Exception? innerException)
    {
        return new MessagingThrottledException(
            FormattableString.Invariant($"The broker is throttling requests on '{entity}'."),
            innerException)
        {
            Entity = entity
        };
    }
}
