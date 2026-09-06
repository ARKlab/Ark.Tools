// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Thrown when the broker no longer holds the lock a delivery was operating on.</summary>
/// <remarks>
/// Transports raise this instead of a provider-specific error so the host can tell "somebody else
/// owns this message now" from a generic transport failure. The delivery is redelivered by the
/// broker; nothing must be settled after it.
/// </remarks>
public sealed class MessagingLockLostException : InvalidOperationException
{
    /// <summary>Creates a lock-lost failure.</summary>
    public MessagingLockLostException()
        : base("The lock on the delivery is no longer held.")
    {
    }

    /// <summary>Creates a lock-lost failure with a message.</summary>
    /// <param name="message">The human-readable explanation.</param>
    public MessagingLockLostException(string message)
        : base(message)
    {
    }

    /// <summary>Creates a lock-lost failure with a message and a cause.</summary>
    /// <param name="message">The human-readable explanation.</param>
    /// <param name="innerException">The cause.</param>
    public MessagingLockLostException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the native delivery identifier whose lock was lost.</summary>
    public string DeliveryId { get; internal init; } = string.Empty;

    internal static MessagingLockLostException _forDelivery(string deliveryId, Exception? innerException)
    {
        return new MessagingLockLostException(
            FormattableString.Invariant($"The lock on delivery '{deliveryId}' is no longer held."),
            innerException)
        {
            DeliveryId = deliveryId
        };
    }
}
