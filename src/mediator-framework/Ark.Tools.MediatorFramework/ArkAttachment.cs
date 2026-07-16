// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>
/// A simple <see cref="IArkAttachment"/> that opens its content through a caller-supplied factory,
/// so each <see cref="OpenRead"/> yields a fresh readable stream.
/// </summary>
public sealed class ArkAttachment : IArkAttachment
{
    private readonly Func<Stream> _openRead;

    /// <summary>Initializes a new instance of the <see cref="ArkAttachment"/> class.</summary>
    /// <param name="name">The attachment file name.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="openRead">A factory returning a readable stream over the payload.</param>
    public ArkAttachment(string name, string contentType, Func<Stream> openRead)
    {
        Name = ArkAttachmentName.Sanitize(name ?? throw new ArgumentNullException(nameof(name)));
        ContentType = contentType ?? throw new ArgumentNullException(nameof(contentType));
        _openRead = openRead ?? throw new ArgumentNullException(nameof(openRead));
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string ContentType { get; }

    /// <inheritdoc />
    public Stream OpenRead() => _openRead();
}

internal static class ArkAttachmentName
{
    public static string Sanitize(string name)
    {
        var leafName = Path.GetFileName(name.Replace('\\', '/'));
        var sanitized = new string(leafName.Where(character => !char.IsControl(character)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }
}
