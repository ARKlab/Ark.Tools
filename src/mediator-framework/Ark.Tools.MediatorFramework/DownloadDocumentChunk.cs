// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.MediatorFramework;

/// <summary>Identifies an attachment for a streamed download.</summary>
[ProtoContract]
public sealed class DownloadDocumentQuery
{
    /// <summary>Gets or sets the attachment identifier.</summary>
    [ProtoMember(1)]
    public Guid Id { get; set; }
}

/// <summary>Metadata sent as the first message of a streamed document download.</summary>
[ProtoContract]
public sealed class DownloadDocumentMetadata
{
    /// <summary>Gets or sets the attachment file name.</summary>
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the MIME content type.</summary>
    [ProtoMember(2)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the payload length when known.</summary>
    [ProtoMember(3)]
    public long? Length { get; set; }
}

/// <summary>A metadata-first or data chunk in a streamed document download.</summary>
[ProtoContract]
public sealed class DownloadDocumentChunk
{
    /// <summary>Gets or sets the download metadata; this is present on the first chunk.</summary>
    [ProtoMember(1)]
    public DownloadDocumentMetadata? Metadata { get; set; }

    /// <summary>Gets or sets the payload bytes for a data chunk.</summary>
    [ProtoMember(2)]
    public byte[]? Data { get; set; }
}
