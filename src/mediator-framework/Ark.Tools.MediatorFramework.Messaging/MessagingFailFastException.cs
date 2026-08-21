// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Represents a bounded, classified messaging failure.</summary>
public sealed class MessagingFailFastException : Exception
{
    /// <summary>Creates a malformed-header messaging failure.</summary>
    public MessagingFailFastException()
        : this(MessagingFailFastReason.MalformedHeaders)
    {
    }

    /// <summary>Creates a messaging failure with a detail message.</summary>
    /// <param name="message">The diagnostic detail.</param>
    public MessagingFailFastException(string message)
        : this(MessagingFailFastReason.MalformedHeaders, message)
    {
    }

    /// <summary>Creates a messaging failure with an inner exception.</summary>
    /// <param name="message">The diagnostic detail.</param>
    /// <param name="innerException">The underlying exception.</param>
    public MessagingFailFastException(string message, Exception innerException)
        : base(_bounded(message), innerException)
    {
        Reason = MessagingFailFastReason.MalformedHeaders;
    }

    /// <summary>Creates a messaging failure.</summary>
    /// <param name="reason">The failure classification.</param>
    /// <param name="detail">Optional bounded diagnostic detail.</param>
    public MessagingFailFastException(MessagingFailFastReason reason, string? detail = null)
        : base(_bounded(detail))
    {
        Reason = reason;
    }

    /// <summary>Gets the failure classification.</summary>
    public MessagingFailFastReason Reason { get; }

    private static string? _bounded(string? detail)
    {
        return detail is null || detail.Length <= 256 ? detail : detail[..256];
    }
}
