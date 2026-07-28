// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>In-memory attachment storage used by the sample.</summary>
public sealed class DocumentStore
{
    private readonly ConcurrentDictionary<Guid, Document> _documents = new();

    /// <summary>Saves an attachment under its correlation identifier.</summary>
    /// <returns>The number of bytes saved.</returns>
    public async Task<long> SaveAsync(Guid id, string name, string contentType, Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer).ConfigureAwait(false);
        _documents[id] = new Document(name, contentType, buffer.ToArray());
        return buffer.Length;
    }

    /// <summary>Gets an attachment, or <see langword="null"/> when it does not exist.</summary>
    public IArkAttachment? Get(Guid id)
    {
        return _documents.TryGetValue(id, out var document)
            ? new ArkAttachment(document.Name, document.ContentType, () => new MemoryStream(document.Content, writable: false))
            : null;
    }

    private sealed record Document(string Name, string ContentType, byte[] Content);
}
