// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Functions;

/// <summary>Versioned response exposing an ETag token.</summary>
public sealed record VersionedEchoResponse
{
    /// <summary>Gets the echoed identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the received precondition token.</summary>
    public string? ReceivedETag { get; init; }

    /// <summary>Gets the response version token.</summary>
    [ETag]
    public string? Version { get; init; }
}

/// <summary>Request exercising ETag precondition binding and response ETag emission.</summary>
[HttpEndpoint("PUT", "/api/v{version}/versioned/{id}", AllowAnonymous = true)]
public sealed record VersionedEchoRequest : IRequest<VersionedEchoRequest, VersionedEchoResponse>
{
    /// <summary>Gets the route identifier.</summary>
    [HttpRoute]
    public Guid Id { get; init; }

    /// <summary>Gets the <c>If-Match</c> precondition token.</summary>
    [ETag]
    public string? ETag { get; init; }
}

/// <summary>Uploads a single text attachment.</summary>
[HttpEndpoint(
    "POST",
    "/api/v{version}/files",
    AllowAnonymous = true,
    MaxFileCount = 1,
    AllowedContentTypes = new[] { "text/plain" })]
public sealed record UploadFileRequest : IRequest<UploadFileRequest, EchoResponse>
{
    /// <summary>Gets the uploaded attachment.</summary>
    public required IArkAttachment Attachment { get; init; }
}

/// <summary>Downloads a text attachment named by route.</summary>
[HttpEndpoint("GET", "/api/v{version}/files/{name}", AllowAnonymous = true)]
public sealed record DownloadFileQuery : IQuery<DownloadFileQuery, IArkAttachment>
{
    /// <summary>Gets the attachment name.</summary>
    [HttpRoute]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Handles <see cref="VersionedEchoRequest"/>.</summary>
public sealed class VersionedEchoRequestHandler : IRequestHandler<VersionedEchoRequest, VersionedEchoResponse>
{
    /// <inheritdoc />
    public async Task<VersionedEchoResponse> ExecuteAsync(VersionedEchoRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Task.FromResult(new VersionedEchoResponse
        {
            Id = request.Id,
            ReceivedETag = request.ETag,
            Version = "v2",
        }).ConfigureAwait(false);
    }
}

/// <summary>Handles <see cref="UploadFileRequest"/>.</summary>
public sealed class UploadFileRequestHandler : IRequestHandler<UploadFileRequest, EchoResponse>
{
    /// <inheritdoc />
    public async Task<EchoResponse> ExecuteAsync(UploadFileRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stream = request.Attachment.OpenRead();
        long length;
        await using (stream.ConfigureAwait(false))
        {
            var buffer = new MemoryStream();
            await using (buffer.ConfigureAwait(false))
            {
                await stream.CopyToAsync(buffer, ctk).ConfigureAwait(false);
                length = buffer.Length;
            }
        }

        return new EchoResponse { Message = request.Attachment.Name, Count = (int)length };
    }
}

/// <summary>Handles <see cref="DownloadFileQuery"/>.</summary>
public sealed class DownloadFileQueryHandler : IQueryHandler<DownloadFileQuery, IArkAttachment>
{
    /// <inheritdoc />
    public async Task<IArkAttachment> ExecuteAsync(DownloadFileQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Task.FromResult<IArkAttachment>(new ArkAttachment(
            query.Name,
            "text/plain",
            () => new MemoryStream(Encoding.UTF8.GetBytes("file:" + query.Name)))).ConfigureAwait(false);
    }
}
