// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.OTel;

/// <summary>
/// Configuration options for the <see cref="ArkAdaptiveSampler"/>.
/// </summary>
public sealed class ArkAdaptiveSamplerOptions
{
    /// <summary>
    /// Gets or sets the target number of root traces to export per second, in total across all
    /// operation buckets. Default is 1.0.
    /// </summary>
    /// <remarks>
    /// Spans kept because a parent was sampled or because their trace failed are not limited by
    /// this budget. The adaptive controller converges to it within a few
    /// <see cref="SamplingPercentageDecreaseTimeout"/> periods; until the first adjustment each
    /// bucket may export up to this rate.
    /// </remarks>
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
    /// When <see langword="true"/>, each distinct operation name gets its own token bucket and
    /// <see cref="TracesPerSecond"/> is shared max-min fairly among the operations seen in the last
    /// <see cref="SamplingPercentageDecreaseTimeout"/> period: an operation slower than the fair share
    /// keeps all its roots, and the rest of the budget is split evenly among the busier operations.
    /// New and idle operations get the current fair share.
    /// When <see langword="false"/>, all roots share one bucket and busy operations can crowd out rare ones.
    /// Default is <see langword="true"/>.
    /// </summary>
    public bool EnablePerOperationBucketing { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of distinct operation buckets to maintain.
    /// Prevents unbounded memory growth when operation names are dynamic: operations beyond the
    /// limit share one overflow bucket, which counts as one operation for the fair share.
    /// Default is 100.
    /// </summary>
    public int MaxOperationBuckets { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether the sampling decision of a remote parent, propagated in the W3C
    /// <c>traceparent</c> sampled flag, is followed. Default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, a span with a sampled remote parent is sampled and a span with an
    /// unsampled remote parent is recorded only, so the caller decides for this service. Use
    /// <see langword="false"/> when callers use a different sampler, such as the classic Application
    /// Insights SDK, whose decisions should not drive this service: a span with a remote parent is then
    /// a root for the local budget and carries this service's sample rate, as with the Azure Monitor
    /// samplers. Distributed traces can then be incomplete across services that decide differently.
    /// Failure promotion applies either way.
    /// </remarks>
    public bool RespectRemoteParentSampling { get; set; } = true;
}
