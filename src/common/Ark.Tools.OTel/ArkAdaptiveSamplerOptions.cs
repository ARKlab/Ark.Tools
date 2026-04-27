// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.OTel;

/// <summary>
/// Configuration options for the <see cref="ArkAdaptiveSampler"/>.
/// </summary>
public sealed class ArkAdaptiveSamplerOptions
{
    /// <summary>
    /// Gets or sets the target number of traces to export per second per operation bucket.
    /// Default is 1.0.
    /// </summary>
    public double TracesPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the moving average ratio for smoothing adaptive rate changes.
    /// Value between 0 (instant adaptation) and 1 (no adaptation). Default is 0.5.
    /// </summary>
    public double MovingAverageRatio { get; set; } = 0.5;

    /// <summary>
    /// Gets or sets how often the adaptive rate controller re-evaluates the sampling percentage.
    /// Default is 1 minute.
    /// </summary>
    public TimeSpan SamplingPercentageDecreaseTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets whether to use per-operation token buckets for fair sampling.
    /// When <see langword="true"/>, each distinct operation name gets its own rate budget.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnablePerOperationBucketing { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of distinct operation buckets to maintain.
    /// Prevents unbounded memory growth when operation names are dynamic.
    /// Default is 100.
    /// </summary>
    public int MaxOperationBuckets { get; set; } = 100;
}
