// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Materializes metadata-delimited files from a client upload stream.</summary>
public static class StreamingArkAttachmentCollection
{
    /// <summary>
    /// Reads all metadata-delimited files from the stream, preserving their order.
    /// </summary>
    /// <param name="chunks">The upload chunks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The uploaded attachments.</returns>
    public static async Task<IReadOnlyList<IArkAttachment>> ReadAllAsync(
        IAsyncEnumerable<UploadDocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        var attachments = new List<IArkAttachment>();
        MemoryStream? content = null;
        UploadDocumentMetadata? metadata = null;

        await foreach (var chunk in chunks.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (chunk.Metadata is not null)
            {
                if (metadata is not null && content is not null)
                    attachments.Add(CreateAttachment(metadata, content));
                metadata = chunk.Metadata;
                content = new MemoryStream();
                continue;
            }

            if (metadata is null || chunk.Data is null)
                throw new InvalidOperationException("Upload chunks must start with metadata and contain data.");
            await content!.WriteAsync(chunk.Data.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (metadata is not null && content is not null)
            attachments.Add(CreateAttachment(metadata, content));
        return attachments;
    }

    private static IArkAttachment CreateAttachment(UploadDocumentMetadata metadata, MemoryStream content)
    {
        var bytes = content.ToArray();
        content.Dispose();
        return new ArkAttachment(metadata.Name, metadata.ContentType, () => new MemoryStream(bytes, writable: false));
    }
}
