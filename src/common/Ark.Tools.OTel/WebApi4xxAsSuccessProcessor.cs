// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// Marks HTTP server spans for 4xx responses as non-errors.
/// </summary>
/// <remarks>
/// Not registered by the Ark setups: register it yourself, before the Ark setup, to keep the old behaviour.
/// It sets the status of every server 4xx span to <see cref="ActivityStatusCode.Unset"/>, also when the
/// application or the instrumentation set <see cref="ActivityStatusCode.Error"/> on purpose.
/// </remarks>
[Obsolete("Clearing Error hides 4xx failures that the application or the instrumentation reported, and Application Insights still counts Unset 4xx requests as failed. Set ActivityStatusCode.Ok in your own processor instead.", UrlFormat = "https://github.com/ARKlab/Ark.Tools/blob/master/docs/otel/sampling.md#http-4xx-status")]
public sealed class WebApi4xxAsSuccessProcessor : BaseProcessor<Activity>
{
    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Kind != ActivityKind.Server)
            return;

        var statusCode = data.GetTagItem("http.response.status_code") switch
        {
            int value => value,
            long value => (int)value,
            string value when int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0
        };

        if (statusCode is >= 400 and < 500)
            data.SetStatus(ActivityStatusCode.Unset);
    }
}
