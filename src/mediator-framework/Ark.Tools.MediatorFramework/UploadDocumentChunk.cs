// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.MediatorFramework;

/// <summary>Metadata sent as the first message of a streamed document upload.</summary>
[ProtoContract]
public sealed class UploadDocumentMetadata
{
    /// <summary>Gets or sets the attachment file name.</summary>
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the MIME content type.</summary>
    [ProtoMember(2)]
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>A metadata-first or data chunk in a streamed document upload.</summary>
[ProtoContract]
public sealed class UploadDocumentChunk
{
    /// <summary>Gets or sets the upload metadata; this must be present on the first chunk.</summary>
    [ProtoMember(1)]
    public UploadDocumentMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the payload bytes for a data chunk.</summary>
    [ProtoMember(2)]
    public byte[]? Data { get; set; }
}

