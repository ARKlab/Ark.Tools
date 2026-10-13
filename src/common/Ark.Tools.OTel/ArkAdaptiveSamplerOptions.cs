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
    /// Gets or sets the moving average ratio for smoothing adaptive rate increases.
    /// Value between 0 (instant adaptation) and 1 (rates never increase). Decreases apply at once,
    /// so the buckets never share more than <see cref="TracesPerSecond"/>. Default is 0.5.
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
    /// gets a refill rate that covers its average traffic, and the rest of the budget is split evenly
    /// among the busier operations. A burst larger than the bucket (two seconds of its rate, at least
    /// one root) is still limited.
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
    /// <c>traceparent</c> sampled flag, is followed. Default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// When <see langword="false"/>, a span with a remote parent is a root for the local budget and
    /// carries this service's sample rate, as with the Azure Monitor samplers, so callers that use a
    /// different sampler, such as the classic Application Insights SDK, do not decide for this service.
    /// Distributed traces can then be incomplete across services that decide differently.
    /// When <see langword="true"/>, a span with a sampled remote parent is sampled without a sample rate
    /// (itemCount 1) and a span with an unsampled remote parent is recorded only.
    /// Failure promotion applies either way.
    /// </remarks>
    public bool RespectRemoteParentSampling { get; set; }
}
