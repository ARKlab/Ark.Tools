// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Response returned after storing an uploaded attachment.</summary>
[ProtoContract]
public sealed record UploadResponse
{
    /// <summary>Gets the upload correlation identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the attachment file name.</summary>
    [ProtoMember(2)]
    public required string Name { get; init; }

    /// <summary>Gets the attachment content type.</summary>
    [ProtoMember(3)]
    public required string ContentType { get; init; }

    /// <summary>Gets the number of bytes received.</summary>
    [ProtoMember(4)]
    public required long Length { get; init; }
}

/// <summary>
/// Pure transport-agnostic request carrying an <see cref="IArkAttachment"/>.
/// </summary>
[HttpEndpoint("POST", "/api/v{version}/greeting-cards/{id}")]
public sealed record UploadGreetingCardRequest : IRequest<UploadResponse>
{
    /// <summary>Gets the upload correlation identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the upload label supplied in the query string.</summary>
    [BindFromQuery]
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the uploaded attachment.</summary>
    public required IArkAttachment Attachment { get; init; }
}

/// <summary>Pure request carrying an ordered collection of attachments.</summary>
[HttpEndpoint("POST", "/api/v{version}/greeting-cards/{id}/batch", MaxFileCount = 4)]
public sealed record UploadGreetingCardsRequest : IRequest<UploadBatchResponse>
{
    /// <summary>Gets the upload correlation identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the uploaded attachments in form order.</summary>
    public IReadOnlyList<IArkAttachment> Attachments { get; init; } = [];
}

/// <summary>Response returned after storing a batch of attachments.</summary>
[ProtoContract]
public sealed record UploadBatchResponse
{
    /// <summary>Gets the upload correlation identifier.</summary>
    [ProtoMember(1)]
    public required Guid Id { get; init; }

    /// <summary>Gets the uploaded file names in order.</summary>
    [ProtoMember(2)]
    public required IReadOnlyList<string> Names { get; init; }
}

/// <summary>Queries a previously uploaded greeting-card attachment.</summary>
[HttpEndpoint("GET", "/api/v{version}/greeting-cards/{id}/download")]
[GrpcMethod("Download")]
[GrpcService("GeneratedDocuments")]
public sealed record GetDocumentQuery : IQuery<IArkAttachment>
{
    /// <summary>Gets the upload correlation identifier.</summary>
    public Guid Id { get; init; }
}
