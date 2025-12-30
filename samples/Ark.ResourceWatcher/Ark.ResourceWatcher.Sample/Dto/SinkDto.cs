// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
namespace Ark.ResourceWatcher.Sample.Dto;

/// <summary>
/// Data transfer object for sending processed data to a sink API.
/// </summary>
public sealed class SinkDto
{
    /// <summary>
    /// Gets or sets the source blob identifier.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Gets or sets the processed records.
    /// </summary>
    public required IReadOnlyList<SinkRecord> Records { get; init; }

    /// <summary>
    /// Gets or sets the total count of records.
    /// </summary>
    public int TotalCount => Records.Count;

    /// <summary>
    /// Gets or sets the processing timestamp.
    /// </summary>
    public DateTimeOffset ProcessedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// A single record in the sink output.
/// </summary>
public sealed record SinkRecord
{
    /// <summary>
    /// Gets or sets the record identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the record name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the record value.
    /// </summary>
    public decimal Value { get; init; }

    /// <summary>
    /// Gets or sets additional properties.
    /// </summary>
    public Dictionary<string, object>? Properties { get; init; }
}
