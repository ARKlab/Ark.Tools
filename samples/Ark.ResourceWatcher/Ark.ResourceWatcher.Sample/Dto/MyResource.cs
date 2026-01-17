// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

namespace Ark.ResourceWatcher.Sample.Dto;

/// <summary>
/// Represents a fetched blob resource with its content.
/// Uses strongly-typed <see cref="MyExtensions"/> for incremental loading support.
/// </summary>
public sealed class MyResource : IResource<MyMetadata, MyExtensions>
{
    /// <summary>
    /// Gets the metadata for this blob.
    /// </summary>
    public required MyMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the binary content of the blob.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// Gets the checksum (hash) of the blob content for change detection.
    /// </summary>
    public string? CheckSum { get; init; }

    /// <summary>
    /// Gets the timestamp when this resource was retrieved.
    /// </summary>
    public Instant RetrievedAt { get; init; }
}