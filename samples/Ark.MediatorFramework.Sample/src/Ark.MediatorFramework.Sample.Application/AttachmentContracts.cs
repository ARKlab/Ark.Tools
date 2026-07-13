// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Response returned after storing an uploaded attachment.</summary>
[ProtoContract]
public sealed record UploadResponse
{
    /// <summary>Gets the attachment file name.</summary>
    [ProtoMember(1)]
    public required string Name { get; init; }

    /// <summary>Gets the attachment content type.</summary>
    [ProtoMember(2)]
    public required string ContentType { get; init; }

    /// <summary>Gets the number of bytes received.</summary>
    [ProtoMember(3)]
    public required long Length { get; init; }
}

/// <summary>
/// Pure transport-agnostic request carrying an <see cref="IArkAttachment"/>.
/// </summary>
[HttpEndpoint("POST", "/api/v{version}/greeting-cards/{id}")]
public sealed record UploadGreetingCardRequest : IRequest<UploadResponse>
{
    /// <summary>Gets the upload correlation identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the upload label supplied in the query string.</summary>
    [BindFromQuery]
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the uploaded attachment.</summary>
    public required IArkAttachment Attachment { get; init; }
}
