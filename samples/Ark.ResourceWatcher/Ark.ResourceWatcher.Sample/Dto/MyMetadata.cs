// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using NodaTime;

namespace Ark.ResourceWatcher.Sample.Dto
{
    /// <summary>
    /// Metadata for a blob resource in external storage.
    /// </summary>
    public sealed class MyMetadata : Ark.Tools.ResourceWatcher.IResourceMetadata
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
        /// Gets optional extension data for incremental loading (e.g., byte offset).
        /// </summary>
        public object? Extensions { get; init; }

        /// <summary>
        /// Gets the content type of the blob.
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// Gets the size of the blob in bytes.
        /// </summary>
        public long? Size { get; init; }
    }
}
