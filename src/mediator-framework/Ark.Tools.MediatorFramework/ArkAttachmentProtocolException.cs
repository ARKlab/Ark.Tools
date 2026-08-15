// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Indicates that an upload stream violates the metadata-first attachment protocol.
/// </summary>
public sealed class ArkAttachmentProtocolException : InvalidOperationException
{
    /// <summary>Initializes a new instance of the <see cref="ArkAttachmentProtocolException"/> class.</summary>
    public ArkAttachmentProtocolException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArkAttachmentProtocolException"/> class.</summary>
    /// <param name="message">The protocol error message.</param>
    public ArkAttachmentProtocolException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArkAttachmentProtocolException"/> class.</summary>
    /// <param name="message">The protocol error message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public ArkAttachmentProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
