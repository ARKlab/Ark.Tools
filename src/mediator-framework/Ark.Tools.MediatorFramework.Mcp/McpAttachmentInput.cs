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
    public ArkAttachment ToAttachment()
    {
        var bytes = Convert.FromBase64String(Blob);
        return new ArkAttachment(Name, MimeType, () => new MemoryStream(bytes, writable: false));
    }
}
