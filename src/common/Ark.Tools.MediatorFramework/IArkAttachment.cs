// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// Transport-agnostic abstraction for a binary attachment (an uploaded/downloaded file).
/// It carries the file <see cref="Name"/>, its <see cref="ContentType"/> and the payload
/// <see cref="Stream"/>, so pure handlers can consume attachments without referencing
/// <c>IFormFile</c>, gRPC streams or any other transport type.
/// </summary>
/// <remarks>
/// This intentionally replaces a generic stream wrapper: an attachment is not a shape parameterized
/// by a payload type, it is always "a named, typed stream of bytes".
/// </remarks>
public interface IArkAttachment
{
    /// <summary>Gets the file name of the attachment (for example <c>invoice.pdf</c>).</summary>
    string Name { get; }

    /// <summary>Gets the MIME content type of the attachment (for example <c>application/pdf</c>).</summary>
    string ContentType { get; }

    /// <summary>Opens the attachment content for reading.</summary>
    /// <returns>A readable <see cref="Stream"/> positioned at the beginning of the payload.</returns>
    Stream OpenRead();
}
