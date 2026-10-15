// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.AspNetCore.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that marks ASP.NET Core HTTP 4xx request spans as non-errors.
/// </summary>
/// <remarks>
/// In REST APIs, client errors (400-499) are typically expected business outcomes rather than
/// server-side failures. This processor clears the error status on 4xx spans so they are
/// not promoted by the failure promotion processor. The ASP.NET Core OpenTelemetry instrumentation
/// already leaves server 4xx responses <c>Unset</c>, so this processor only clears an <c>Error</c>
/// that the application or an unhandled exception set, and Application Insights still counts the
/// <c>Unset</c> 4xx requests as failed.
/// </remarks>
[Obsolete("Clearing Error hides 4xx failures that the application or the instrumentation reported, and Application Insights still counts Unset 4xx requests as failed. Set ActivityStatusCode.Ok in your own processor instead.", UrlFormat = "https://github.com/ARKlab/Ark.Tools/blob/master/docs/otel/sampling.md#http-4xx-status")]
public sealed class WebApi4xxAsSuccessProcessor : BaseProcessor<Activity>
{
    /// <summary>
    /// Initializes a new instance of <see cref="WebApi4xxAsSuccessProcessor"/>.
    /// </summary>
    public WebApi4xxAsSuccessProcessor()
    {
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
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
        {
            // Override the error status to unset so the span is not treated as a failure.
            data.SetStatus(ActivityStatusCode.Unset);
        }
    }
}
