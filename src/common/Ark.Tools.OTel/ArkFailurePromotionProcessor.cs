// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that promotes the entire operation to exported
/// when any span in the operation represents a failure.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ArkAdaptiveSampler"/> returns <see cref="OpenTelemetry.Trace.SamplingDecision.RecordOnly"/>
/// (not <c>Drop</c>) for rate-limited spans. This processor inspects each completed span and, when
/// a failure (a span ending with <see cref="ActivityStatusCode.Error"/> status) is detected, promotes the failing span <b>and its entire parent chain</b> to
/// <see cref="OpenTelemetry.Trace.SamplingDecision.RecordAndSample"/> by restoring the
/// <see cref="ActivityTraceFlags.Recorded"/> flag.
/// </para>
/// <para>
/// The failed <see cref="ActivityTraceId"/> is also registered in a shared <see cref="FailedTraceRegistry"/>.
/// This allows:
/// </para>
/// <list type="bullet">
/// <item><description>
/// Sibling spans that complete <em>after</em> the failure is detected to be promoted in their own <c>OnEnd</c> call.
/// </description></item>
/// <item><description>
/// The <see cref="ArkAdaptiveSampler"/> to immediately return <c>RecordAndSample</c> for any new
/// child spans started after failure detection, without waiting for their parent flags to propagate.
/// </description></item>
/// </list>
/// <para>
/// When a sampled span starts, the processor copies the <c>microsoft.sample_rate</c> attribute of its
/// in-process parent (<see cref="Activity.Parent"/>), so a trace shares the weight of its root. A span
/// started with an explicit parent context has no <see cref="Activity.Parent"/> and carries no rate.
/// </para>
/// <para>
/// Every span exported because of a failure, including an already sampled one, gets the
/// <c>microsoft.sample_rate</c> attribute set to 100 (itemCount 1): its export no longer depends on
/// the sampling budget, so weighting it as a sample would inflate request and failure counts.
/// </para>
/// <para>
/// <b>Limitation:</b> sibling spans that have <em>already completed before</em> the failure is detected
/// cannot be retroactively included; they have already been processed by the export pipeline.
/// The parent (root) span and all spans that complete after failure detection are always captured.
/// </para>
/// </remarks>
public sealed class ArkFailurePromotionProcessor : BaseProcessor<Activity>
{
    private readonly FailedTraceRegistry _registry;

    /// <summary>
    /// Initializes a new instance of <see cref="ArkFailurePromotionProcessor"/> with a standalone
    /// failure-trace registry (no coordination with an external sampler).
    /// </summary>
    public ArkFailurePromotionProcessor()
        : this(new FailedTraceRegistry())
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ArkFailurePromotionProcessor"/> using the supplied
    /// <paramref name="registry"/> so the processor and an <see cref="ArkAdaptiveSampler"/> sharing
    /// the same registry can coordinate whole-operation failure promotion.
    /// </summary>
    /// <param name="registry">The shared registry of failed trace IDs.</param>
    public ArkFailurePromotionProcessor(FailedTraceRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc/>
    public override void OnStart(Activity data)
    {
        // A sampled child shares the sample rate of its parent. Copying the tag here costs one tag,
        // where a sampling attribute would also allocate the SDK's sampling tag collection.
        if (data.Recorded && data.Parent?.GetTagItem(ArkAdaptiveSampler._sampleRateTag) is { } sampleRate)
            data.SetTag(ArkAdaptiveSampler._sampleRateTag, sampleRate);
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (_isFailure(data))
        {
            // Register the entire trace as failed so in-flight siblings and future children are promoted.
            _registry.Register(data.TraceId);

            // Promote the failing span and walk the in-process parent chain to promote all ancestors.
            // Parent spans have not yet ended at this point (children always end before parents
            // in a single-process trace), so setting their flags here means the exporter will
            // include them when their own OnEnd is later called.
            for (var span = data; span != null; span = span.Parent)
                _promoteSpan(span);

            return;
        }

        // Not a failure, but the trace was already identified as failed by an earlier span.
        // Promote this span so the full operation is captured.
        if (_registry.IsFailed(data.TraceId))
            _promoteSpan(data);
    }

    private static void _promoteSpan(Activity activity)
    {
        // Keep the sampler's decision for ArkAdaptiveSampler._release: the tag below no longer reflects it.
        if (activity.GetCustomProperty(ArkAdaptiveSampler._bucketSampleRateProperty) is null)
        {
            activity.SetCustomProperty(
                ArkAdaptiveSampler._bucketSampleRateProperty,
                activity.GetTagItem(ArkAdaptiveSampler._sampleRateTag) as double? ?? 0.0);
        }

        activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
        activity.IsAllDataRequested = true;

        // The failure makes the export certain, whatever the sampler decided: the span counts only itself.
        activity.SetTag(ArkAdaptiveSampler._sampleRateTag, 100.0);
    }

    // The producer of a span decides whether it failed: an Unset or Ok span is not a failure,
    // whatever its exception events or response codes.
    private static bool _isFailure(Activity activity)
    {
        return activity.Status == ActivityStatusCode.Error;
    }
}
