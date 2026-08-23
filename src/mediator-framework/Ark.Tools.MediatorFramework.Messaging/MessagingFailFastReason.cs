// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Classifies failures that must not be retried as normal handler failures.</summary>
public enum MessagingFailFastReason
{
    /// <summary>The content type is not registered.</summary>
    UnknownContentType,

    /// <summary>The serialization protocol is not registered.</summary>
    UnknownProtocol,

    /// <summary>The content encoding is not supported.</summary>
    UnsupportedContentEncoding,

    /// <summary>The logical contract name is not registered.</summary>
    UnknownContractName,

    /// <summary>The message belongs to another network.</summary>
    ForeignNetwork,

    /// <summary>The required headers are malformed.</summary>
    MalformedHeaders,

    /// <summary>The headers exceed configured bounds.</summary>
    OversizedHeaders,

    /// <summary>The payload exceeds the network limit.</summary>
    OversizedPayload,

    /// <summary>The payload attachment failed integrity validation.</summary>
    AttachmentIntegrityFailure,

    /// <summary>The required second-level handler is missing.</summary>
    MissingSecondLevelHandler,

    /// <summary>The configured payload compression or decompression failed.</summary>
    InvalidCompressedPayload
}
