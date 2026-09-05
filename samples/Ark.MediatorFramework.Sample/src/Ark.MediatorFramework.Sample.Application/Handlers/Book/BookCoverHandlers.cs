// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Stores uploaded book covers.</summary>
public sealed class UploadBookCoverHandler : IRequestHandler<UploadBookCoverRequest.V1, UploadResponse>
{
    private readonly DocumentStore _documents;

    /// <summary>Initializes a new instance of the <see cref="UploadBookCoverHandler"/> class.</summary>
    /// <param name="documents">The attachment store.</param>
    public UploadBookCoverHandler(DocumentStore documents)
    {
        _documents = documents;
    }

    /// <inheritdoc />
    public async Task<UploadResponse> ExecuteAsync(
        UploadBookCoverRequest.V1 request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stream = request.Attachment.OpenRead();
        await using var __ctx = stream.ConfigureAwait(false);
        var length = await _documents.SaveAsync(
            request.Id,
            request.Attachment.Name,
            request.Attachment.ContentType,
            stream).ConfigureAwait(false);

        return new UploadResponse
        {
            Id = request.Id,
            Name = request.Attachment.Name,
            ContentType = request.Attachment.ContentType,
            Length = length,
        };
    }
}

/// <summary>Loads stored book covers.</summary>
public sealed class DownloadBookCoverHandler : IQueryHandler<DownloadBookCoverQuery.V1, IArkAttachment>
{
    private readonly DocumentStore _documents;

    /// <summary>Initializes a new instance of the <see cref="DownloadBookCoverHandler"/> class.</summary>
    /// <param name="documents">The attachment store.</param>
    public DownloadBookCoverHandler(DocumentStore documents)
    {
        _documents = documents;
    }

    /// <inheritdoc />
    public async Task<IArkAttachment> ExecuteAsync(
        DownloadBookCoverQuery.V1 query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await Task.FromResult(_documents.Get(query.Id)
            ?? throw new EntityNotFoundException($"Book cover '{query.Id}' was not found.")).ConfigureAwait(false);
    }
}
