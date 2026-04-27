// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;

using OpenTelemetry;

using System.Diagnostics;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that enriches HTTP request spans with the
/// authenticated user's identity.
/// </summary>
public sealed class WebApiUserProcessor : BaseProcessor<Activity>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of <see cref="WebApiUserProcessor"/>.
    /// </summary>
    public WebApiUserProcessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        var httpCtx = _httpContextAccessor.HttpContext;
        if (httpCtx == null) return;

        var identity = httpCtx.User?.Identity;
        if (identity?.IsAuthenticated != true) return;

        if (data.GetTagItem("enduser.id") != null) return;

        string? userId = null;
        if (!string.IsNullOrWhiteSpace(identity.Name))
        {
            userId = identity.Name;
        }
        else if (identity is ClaimsIdentity ci)
        {
            userId = ci.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        if (!string.IsNullOrWhiteSpace(userId))
            data.SetTag("enduser.id", userId);
    }
}
