// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher.Testing;

/// <summary>
/// Example typed extension data for testing purposes.
/// Demonstrates common properties used in resource state extensions.
/// </summary>
public record TestExtensions
{
    /// <summary>
    /// Gets or sets the last processed offset (for append-only or streaming resources).
    /// </summary>
    public long LastOffset { get; init; }

    /// <summary>
    /// Gets or sets the ETag from the last successful fetch.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// Gets or sets additional metadata key-value pairs.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Gets or sets a counter for testing purposes.
    /// </summary>
    public int Counter { get; init; }
}

/// <summary>
/// JSON source generation context for TestExtensions.
/// Use this when testing with Native AoT or trimmed scenarios.
/// </summary>
/// <remarks>
/// Note: ResourceState serialization is not included because it contains an Exception property
/// which is not compatible with source-generated JSON contexts. In real scenarios, use
/// SqlStateProvider with appropriate JsonSerializerOptions for extension serialization only.
/// </remarks>
[JsonSerializable(typeof(TestExtensions))]
public partial class TestExtensionsJsonContext : JsonSerializerContext
{
}
