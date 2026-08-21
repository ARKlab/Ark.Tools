// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Represents a bounded, classified messaging failure.</summary>
public sealed class MessagingFailFastException : Exception
{
    /// <summary>Creates a messaging failure.</summary>
    /// <param name="reason">The failure classification.</param>
    /// <param name="detail">Optional bounded diagnostic detail.</param>
    public MessagingFailFastException(MessagingFailFastReason reason, string? detail = null)
        : base(detail)
    {
        Reason = reason;
    }

    /// <summary>Gets the failure classification.</summary>
    public MessagingFailFastReason Reason { get; }
}
