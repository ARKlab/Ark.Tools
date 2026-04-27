// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that promotes spans to exported if they
/// represent a failure, even when the sampler initially decided not to export them.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ArkAdaptiveSampler"/> returns <see cref="OpenTelemetry.Trace.SamplingDecision.RecordOnly"/>
/// (not <c>Drop</c>) for rate-limited spans. This allows this processor to inspect the completed
/// span and upgrade spans that represent errors or exceptions to
/// <see cref="OpenTelemetry.Trace.SamplingDecision.RecordAndSample"/> by restoring the
/// <see cref="ActivityTraceFlags.Recorded"/> flag.
/// </para>
/// </remarks>
public sealed class ArkFailurePromotionProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        // Only act on spans that were NOT originally sampled.
        if (data.Recorded)
            return;

        if (IsFailure(data))
        {
            // Promote to sampled so the exporter will include this span.
            data.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            data.IsAllDataRequested = true;
        }
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

        if (statusCodeTag is string httpCodeStr && int.TryParse(httpCodeStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedCode) && parsedCode >= 400)
            return true;

        // gRPC status codes (0 = OK).
        var grpcStatus = activity.GetTagItem("rpc.grpc.status_code");
        if (grpcStatus is int grpcCode && grpcCode != 0)
            return true;

        if (grpcStatus is string grpcStr && int.TryParse(grpcStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedGrpc) && parsedGrpc != 0)
            return true;

        return false;
    }
}
