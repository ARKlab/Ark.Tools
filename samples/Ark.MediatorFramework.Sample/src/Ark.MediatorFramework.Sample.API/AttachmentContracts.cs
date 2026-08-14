// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Ark.MediatorFramework.Sample.API.Authorization;

using ProtoBuf;

namespace Ark.MediatorFramework.Sample.API;

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
public sealed record UploadGreetingCardRequest : IRequest<UploadGreetingCardRequest, UploadResponse>
{
    /// <summary>Gets the upload correlation identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the upload label supplied in the query string.</summary>
    [HttpQuery]
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets the uploaded attachment.</summary>
    public required IArkAttachment Attachment { get; init; }
}

/// <summary>Uploads a cover for a book.</summary>
[HttpEndpoint(
    "POST",
    "/api/v{version}/books/{id}/cover",
    MaxFileCount = 1,
    AllowedContentTypes = new[] { "image/jpeg", "image/png" })]
[RequireScopePolicy(ApplicationScopes.BookCover)]
public sealed record UploadBookCoverRequest : IRequest<UploadBookCoverRequest, UploadResponse>
{
    /// <summary>Gets the book identifier.</summary>
    [HttpRoute]
    public Guid Id { get; init; }

    /// <summary>Gets the uploaded cover attachment.</summary>
    public required IArkAttachment Attachment { get; init; }
}

/// <summary>Pure request carrying an ordered collection of attachments.</summary>
[HttpEndpoint("POST", "/api/v{version}/greeting-cards/{id}/batch", MaxFileCount = 4)]
public sealed record UploadGreetingCardsRequest : IRequest<UploadGreetingCardsRequest, UploadBatchResponse>
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
public sealed record GetDocumentQuery : IQuery<GetDocumentQuery, IArkAttachment>
{
    /// <summary>Gets the upload correlation identifier.</summary>
    public Guid Id { get; init; }
}

/// <summary>Downloads the cover for a book.</summary>
[HttpEndpoint("GET", "/api/v{version}/books/{id}/cover")]
[GrpcMethod("DownloadBookCover")]
[GrpcService("Books")]
[RequireScopePolicy(ApplicationScopes.BookCover)]
public sealed record DownloadBookCoverQuery : IQuery<DownloadBookCoverQuery, IArkAttachment>
{
    /// <summary>Gets the book identifier.</summary>
    [HttpRoute]
    public Guid Id { get; init; }
}
