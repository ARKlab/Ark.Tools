// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;

using OpenTelemetry;

using System.Diagnostics;

namespace Ark.Tools.AspNetCore.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that marks HTTP 4xx spans as non-errors.
/// </summary>
/// <remarks>
/// In REST APIs, client errors (400-499) are typically expected business outcomes rather than
/// server-side failures. This processor clears the error status on 4xx spans so they are
/// not counted as errors in Application Insights and are not promoted by the failure promotion processor.
/// </remarks>
public sealed class WebApi4xxAsSuccessProcessor : BaseProcessor<Activity>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="WebApi4xxAsSuccessProcessor"/>.
    /// </summary>
    public WebApi4xxAsSuccessProcessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        var statusCode = _httpContextAccessor.HttpContext?.Response.StatusCode;
        if (statusCode is >= 400 and < 500)
        {
            // Override the error status to unset so the span is not treated as a failure.
            data.SetStatus(ActivityStatusCode.Unset);
        }
    }
}
