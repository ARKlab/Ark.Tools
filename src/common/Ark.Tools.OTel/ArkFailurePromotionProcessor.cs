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
/// a failure is detected, promotes the failing span <b>and its entire parent chain</b> to
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
    public override void OnEnd(Activity data)
    {
        // If this span is already sampled, it will be exported normally.
        // Still register a failure so in-flight siblings and future children are promoted.
        if (data.Recorded)
        {
            if (IsFailure(data))
                _registry.Register(data.TraceId);
            return;
        }

        // This span was rate-limited (RecordOnly). Decide whether to promote.

        if (IsFailure(data))
        {
            // Register the entire trace as failed.
            _registry.Register(data.TraceId);

            // Promote the failing span itself.
            PromoteSpan(data);

            // Walk the in-process parent chain and promote all ancestors.
            // Parent spans have not yet ended at this point (children always end before parents
            // in a single-process trace), so setting their flags here means the exporter will
            // include them when their own OnEnd is later called.
            var parent = data.Parent;
            while (parent != null)
            {
                if (!parent.Recorded)
                    PromoteSpan(parent);
                parent = parent.Parent;
            }

            return;
        }

        // Not a failure, but the trace was already identified as failed by an earlier span.
        // Promote this span so the full operation is captured.
        if (_registry.IsFailed(data.TraceId))
            PromoteSpan(data);
    }

    private static void PromoteSpan(Activity activity)
    {
        activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
        activity.IsAllDataRequested = true;
    }

    private static bool IsFailure(Activity activity)
    {
        // Error status set explicitly.
        if (activity.Status == ActivityStatusCode.Error)
            return true;

        // Exception events recorded on the span.
        foreach (var evt in activity.Events)
        {
            if (string.Equals(evt.Name, "exception", StringComparison.Ordinal))
                return true;
        }

        // HTTP response status code >= 400.
        var statusCodeTag = activity.GetTagItem("http.response.status_code");
        if (statusCodeTag is int httpCode && httpCode >= 400)
            return true;

        if (statusCodeTag is string httpCodeStr
            && int.TryParse(httpCodeStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedCode)
            && parsedCode >= 400)
            return true;

        // gRPC status codes (0 = OK).
        var grpcStatus = activity.GetTagItem("rpc.grpc.status_code");
        if (grpcStatus is int grpcCode && grpcCode != 0)
            return true;

        if (grpcStatus is string grpcStr
            && int.TryParse(grpcStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedGrpc)
            && parsedGrpc != 0)
            return true;

        return false;
    }
}
