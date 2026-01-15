// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using NodaTime;


namespace Ark.Tools.ResourceWatcher.Testing;


/// <summary>
/// Stub resource metadata for testing purposes.
/// Implements all IResourceMetadata properties with testable defaults.
/// </summary>
public sealed class StubResourceMetadata : IResourceMetadata
{
    /// <summary>
    /// Gets or sets the resource identifier.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets or sets the last modified timestamp.
    /// </summary>
    public required LocalDateTime Modified { get; init; }

    /// <summary>
    /// Gets or sets the per-source modified timestamps.
    /// </summary>
    public Dictionary<string, LocalDateTime>? ModifiedSources { get; init; }

    /// <summary>
    /// Gets or sets extension data for the resource.
    /// </summary>
    public VoidExtensions? Extensions { get; init; }

    /// <summary>
    /// Creates a new metadata with incremented Modified time.
    /// </summary>
    /// <param name="increment">The time increment to add.</param>
    /// <returns>A new metadata instance with updated timestamp.</returns>
    public StubResourceMetadata WithIncrement(Duration increment)
    {
        return new StubResourceMetadata
        {
            ResourceId = ResourceId,
            Modified = Modified.PlusTicks((long)increment.TotalTicks),
            ModifiedSources = ModifiedSources,
            Extensions = Extensions
        };
    }

    /// <summary>
    /// Creates a new metadata with updated source timestamp.
    /// </summary>
    /// <param name="source">The source identifier.</param>
    /// <param name="modified">The new modified timestamp for the source.</param>
    /// <returns>A new metadata instance with updated source timestamp.</returns>
    public StubResourceMetadata WithSourceModified(string source, LocalDateTime modified)
    {
        var sources = ModifiedSources != null
            ? new Dictionary<string, LocalDateTime>(ModifiedSources, StringComparer.Ordinal) { [source] = modified }
            : new Dictionary<string, LocalDateTime>(StringComparer.Ordinal) { [source] = modified };
        return new StubResourceMetadata
        {
            ResourceId = ResourceId,
            Modified = Modified,
            ModifiedSources = sources,
            Extensions = Extensions
        };
    }
}