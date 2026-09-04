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

/// <summary>Uploads a cover for a book.</summary>
public static class UploadBookCoverRequest
{
    /// <summary>Version one of the book-cover upload request.</summary>
    [HttpEndpoint(
        "POST",
        "/api/v{version}/books/{id}/cover",
        MaxFileCount = 1,
        AllowedContentTypes = new[] { "image/jpeg", "image/png" })]
    [RequireScopePolicy(ApplicationScopes.BookCover)]
    public sealed record V1 : IRequest<V1, UploadResponse>
    {
        /// <summary>Gets the book identifier.</summary>
        [HttpRoute]
        public Guid Id { get; init; }

        /// <summary>Gets the uploaded cover attachment.</summary>
        public required IArkAttachment Attachment { get; init; }
    }
}

/// <summary>Downloads the cover for a book.</summary>
public static class DownloadBookCoverQuery
{
    /// <summary>Version one of the book-cover download query.</summary>
    [HttpEndpoint("GET", "/api/v{version}/books/{id}/cover")]
    [GrpcMethod("DownloadBookCover")]
    [GrpcService("Books")]
    [RequireScopePolicy(ApplicationScopes.BookCover)]
    public sealed record V1 : IQuery<V1, IArkAttachment>
    {
        /// <summary>Gets the book identifier.</summary>
        [HttpRoute]
        public Guid Id { get; init; }
    }
}
