// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ModelContextProtocol.Protocol;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Converts mediator attachments to bounded MCP embedded resources.</summary>
public static class McpAttachmentResults
{
    /// <summary>Reads an attachment and returns it as one MCP embedded resource.</summary>
    /// <param name="attachment">The attachment to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="maximumBytes">The maximum number of bytes to materialize.</param>
    /// <returns>The MCP tool result containing the attachment.</returns>
    public static async Task<CallToolResult> ToToolResultAsync(
        IArkAttachment attachment,
        long maximumBytes = 10_000_000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var input = attachment.OpenRead();
        await using var _ = input.ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maximumBytes)
                throw new InvalidOperationException("The attachment exceeds the configured download limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        var resource = new BlobResourceContents
        {
            Uri = "ark://" + Guid.NewGuid().ToString("N") + "/" + Uri.EscapeDataString(attachment.Name),
            MimeType = attachment.ContentType,
            Blob = output.ToArray(),
        };
        return new CallToolResult
        {
            Content = [new EmbeddedResourceBlock { Resource = resource }],
        };
    }
}
