// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Represents an inline, bounded MCP attachment upload.</summary>
public sealed class McpAttachmentInput
{
    /// <summary>Gets or sets the client-provided file name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the MIME content type.</summary>
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "application/octet-stream";

    /// <summary>Gets or sets the base64 encoded content.</summary>
    [JsonPropertyName("blob")]
    public string Blob { get; set; } = string.Empty;

    /// <summary>Converts the input to the transport-neutral attachment abstraction.</summary>
    /// <returns>An attachment backed by the decoded bytes.</returns>
    public ArkAttachment ToAttachment(long maximumBytes = 10_000_000, IReadOnlySet<string>? allowedContentTypes = null)
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Attachment name is required.");
        if (string.IsNullOrWhiteSpace(MimeType))
            throw new ArgumentException("Attachment MIME type is required.");
        if (allowedContentTypes is not null && !allowedContentTypes.Contains(MimeType))
            throw new InvalidOperationException("The attachment MIME type is not allowed.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(Blob);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Attachment blob must be base64.", exception);
        }
        if (bytes.LongLength > maximumBytes)
            throw new InvalidOperationException("The attachment exceeds the configured size limit.");
        return new ArkAttachment(Name, MimeType, () => new MemoryStream(bytes, writable: false));
    }
}
