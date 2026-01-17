// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher;
using NodaTime;

namespace Ark.ResourceWatcher.Sample.Dto;

/// <summary>
/// Metadata for a blob resource in external storage.
/// Uses strongly-typed <see cref="MyExtensions"/> for incremental loading support.
/// </summary>
public sealed class MyMetadata : IResourceMetadata<MyExtensions>
{
    /// <summary>
    /// Gets the unique identifier for the blob (typically the blob path).
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the last modified timestamp of the blob.
    /// </summary>
    public LocalDateTime Modified { get; init; }

    /// <summary>
    /// Gets the optional per-source modified timestamps for composite resources.
    /// </summary>
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }

    /// <summary>
    /// Gets strongly-typed extension data for incremental loading.
    /// Supports tracking byte offsets, ETags, and sync timestamps.
    /// </summary>
    public MyExtensions? Extensions { get; init; }

    /// <summary>
    /// Gets the content type of the blob.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the size of the blob in bytes.
    /// </summary>
    public long? Size { get; init; }
}