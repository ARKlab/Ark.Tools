// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Response returned after storing an uploaded attachment.</summary>
public sealed record UploadResponse
{
    /// <summary>Gets the attachment file name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the attachment content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>Gets the number of bytes received.</summary>
    public required long Length { get; init; }
}

/// <summary>
/// Pure transport-agnostic request carrying an <see cref="IArkAttachment"/>. It is intentionally
/// not source-generated (attachments require a multipart binding); the hosting assembly exposes it
/// through a hand-written endpoint that maps the uploaded file into the attachment abstraction.
/// </summary>
public sealed record UploadGreetingCardRequest : IRequest<UploadResponse>
{
    /// <summary>Gets the uploaded attachment.</summary>
    public required IArkAttachment Attachment { get; init; }
}
