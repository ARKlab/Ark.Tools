// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using NodaTime;

namespace Ark.ResourceWatcher.Sample.Dto;

/// <summary>
/// Strongly-typed extension data for blob resources to support incremental loading.
/// This demonstrates the type-safe extensions feature in ResourceWatcher.
/// </summary>
/// <remarks>
/// By using a strongly-typed extension model instead of <c>object?</c>, we get:
/// <list type="bullet">
/// <item><description>Compile-time type safety - no runtime casting or type checking</description></item>
/// <item><description>IntelliSense support - IDE autocomplete for all properties</description></item>
/// <item><description>AoT compatibility - works with Native AoT when using source-generated JSON context</description></item>
/// <item><description>Refactoring safety - rename/delete operations are caught at compile time</description></item>
/// </list>
/// </remarks>
public sealed record MyExtensions
{
    /// <summary>
    /// Gets the byte offset of the last processed position in an append-only blob.
    /// Used for incremental loading of log files, event streams, or other append-only resources.
    /// </summary>
    /// <example>
    /// If a 10MB log file was previously processed up to 5MB, LastProcessedOffset would be 5242880.
    /// On the next fetch, we can request bytes from offset 5242880 onwards.
    /// </example>
    public long? LastProcessedOffset { get; init; }

    /// <summary>
    /// Gets the ETag from the last successful fetch.
    /// Used to detect if the blob has changed since last processing.
    /// </summary>
    /// <remarks>
    /// ETags are typically MD5 hashes or version identifiers provided by blob storage.
    /// By storing the ETag, we can use conditional requests (If-None-Match) to avoid
    /// downloading unchanged blobs.
    /// </remarks>
    public string? LastETag { get; init; }

    /// <summary>
    /// Gets the timestamp of the last successful synchronization.
    /// Useful for tracking staleness and implementing time-based refresh policies.
    /// </summary>
    public Instant? LastSuccessfulSync { get; init; }

    /// <summary>
    /// Gets custom metadata key-value pairs for blob-specific tracking.
    /// Can store additional context like processing statistics, source system identifiers, etc.
    /// </summary>
    /// <example>
    /// <code>
    /// Metadata = new Dictionary&lt;string, string&gt;
    /// {
    ///     ["SourceSystem"] = "ProductionAPI",
    ///     ["LinesProcessed"] = "1524",
    ///     ["LastErrorCode"] = "200"
    /// }
    /// </code>
    /// </example>
    public Dictionary<string, string>? Metadata { get; init; }
}
