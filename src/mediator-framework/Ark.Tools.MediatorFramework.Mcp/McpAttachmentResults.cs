// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Buffers;

using ModelContextProtocol.Protocol;

using FluentValidation;
using FluentValidation.Results;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Converts mediator attachments to bounded MCP embedded resources.</summary>
public static class McpAttachmentResults
{
    /// <summary>Reads an attachment and returns it as one MCP embedded resource.</summary>
    /// <param name="attachment">The attachment to read.</param>
    /// <param name="maximumBytes">The maximum number of bytes to materialize.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The MCP embedded resource containing the attachment.</returns>
    public static async Task<EmbeddedResourceBlock> ToEmbeddedResourceAsync(
        IArkAttachment attachment,
        long maximumBytes = 10_000_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var input = attachment.OpenRead();
        await using var _ = input.ConfigureAwait(false);
        var payload = input.CanSeek
            ? await _readSeekableAsync(input, maximumBytes, cancellationToken).ConfigureAwait(false)
            : await _readUnseekableAsync(input, maximumBytes, cancellationToken).ConfigureAwait(false);

        // Blob carries the base64 text as UTF-8; FromBytes encodes the raw attachment bytes.
        var resource = BlobResourceContents.FromBytes(
            payload,
            "ark://" + Guid.NewGuid().ToString("N") + "/" + Uri.EscapeDataString(attachment.Name),
            attachment.ContentType);
        return new EmbeddedResourceBlock
        {
            Resource = resource,
        };
    }

    private static async Task<ReadOnlyMemory<byte>> _readSeekableAsync(
        Stream input,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        // The length is known up front: reject before reading and allocate the exact payload size.
        var remaining = input.Length - input.Position;
        // Some seekable streams (e.g. Linux /proc files) report 0 yet yield data: stream them with the limit.
        if (remaining == 0)
            return await _readUnseekableAsync(input, maximumBytes, cancellationToken).ConfigureAwait(false);
        if (remaining > maximumBytes)
            throw _tooLarge();
        var payload = new byte[remaining];
        await input.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task<ReadOnlyMemory<byte>> _readUnseekableAsync(
        Stream input,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        const int ChunkSize = 81920;
        var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
        try
        {
            int read;
            // Rent may return a larger array; cap each read so the MemoryStream grows in fixed chunks.
            while ((read = await input.ReadAsync(buffer.AsMemory(0, ChunkSize), cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (output.Length + read > maximumBytes)
                    throw _tooLarge();
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // Expose the MemoryStream buffer directly to avoid copying the final payload.
        return output.GetBuffer().AsMemory(0, (int)output.Length);
    }

    private static ValidationException _tooLarge()
    {
        return new ValidationException(
            [new ValidationFailure("attachment", "The attachment exceeds the configured download limit.")]);
    }
}
