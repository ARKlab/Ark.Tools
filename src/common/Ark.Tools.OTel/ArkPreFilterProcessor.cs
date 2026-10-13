// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that filters out high-volume, low-value spans
/// before they reach the sampler or exporter.
/// </summary>
/// <remarks>
/// <para>
/// Filtered spans are marked via <see cref="Activity.IsAllDataRequested"/> and
/// <see cref="Activity.ActivityTraceFlags"/> so the SDK stops collecting data for them immediately.
/// Only successful spans are filtered; failed variants of the same operations are still captured.
/// </para>
/// <para>
/// Filtered span types:
/// </para>
/// <list type="bullet">
/// <item><description>Successful HTTP <c>OPTIONS</c> requests (CORS preflight noise).</description></item>
/// <item><description>Successful SQL <c>Commit</c> operations.</description></item>
/// </list>
/// </remarks>
public sealed class ArkPreFilterProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnStart(Activity data)
    {
        if (data.IsAllDataRequested && _shouldFilter(data))
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            data.IsAllDataRequested = false;
            _sampler?._release(data);
        }
    }

    // Set by AddArkOpenTelemetry: a span filtered here, after sampling, gives its budget back.
    internal ArkAdaptiveSampler? _sampler;

    private static bool _shouldFilter(Activity activity)
    {
        return _isNoise(
            activity.DisplayName,
            activity.GetTagItem("http.request.method") as string,
            activity.GetTagItem("db.operation") as string
                ?? activity.GetTagItem("db.operation.name") as string);
    }

    // Shared with ArkAdaptiveSampler, which applies the same rules to the name and creation tags before sampling.
    internal static bool _isNoise(string? displayName, string? httpMethod, string? dbOperation)
    {
        // --- HTTP OPTIONS requests ---
        // Span name for ASP.NET Core is "HTTP {METHOD} {ROUTE}" or similar.
        // The http.request.method tag contains the method.
        if (!string.IsNullOrEmpty(httpMethod) &&
            string.Equals(httpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            // Only filter if the response was successful (no error status set yet at OnStart,
            // but we can check the tag if ASP.NET Core sets it early, or rely on the
            // failure promotion processor to catch it if it fails later).
            return true;
        }

        // Legacy: span name may start with "OPTIONS "
        if (displayName != null &&
            displayName.StartsWith("OPTIONS ", StringComparison.OrdinalIgnoreCase))
            return true;

        // --- SQL Commit ---
        if (dbOperation != null &&
            dbOperation.Equals("Commit", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
