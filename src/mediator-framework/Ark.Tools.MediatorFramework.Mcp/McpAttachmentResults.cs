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
        var payload = await _readAsync(input, maximumBytes, cancellationToken).ConfigureAwait(false);

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

    private static async Task<ReadOnlyMemory<byte>> _readAsync(
        Stream input,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        // A seekable stream's length only presizes the buffer: it can be wrong (e.g. Linux /proc files report 0)
        // or change while being read, so the limit is always enforced on the bytes actually read.
        var expected = input.CanSeek ? input.Length - input.Position : 0;
        if (expected > maximumBytes)
            throw _tooLarge();
        using var output = new MemoryStream(expected > 0 ? (int)expected : 0);
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
            // The chunk held attachment bytes: clear it before other pool renters can see it.
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
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
