// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using OpenTelemetry;

using System.Diagnostics;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore.ApplicationInsights;

/// <summary>
/// An OpenTelemetry <see cref="BaseProcessor{T}"/> that enriches ASP.NET Core HTTP request spans
/// with the authenticated user's stable identifier.
/// </summary>
public sealed class WebApiUserProcessor : BaseProcessor<Activity>
{
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    /// <summary>
    /// Initializes a new instance of <see cref="WebApiUserProcessor"/>.
    /// </summary>
    /// <param name="userContext">The current user context.</param>
    public WebApiUserProcessor(IContextProvider<ClaimsPrincipal> userContext)
    {
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    /// <inheritdoc/>
    public override void OnEnd(Activity data)
    {
        if (data.Kind != ActivityKind.Server)
            return;

        ClaimsPrincipal principal;
        try
        {
            principal = _userContext.Current;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (principal.Identity?.IsAuthenticated != true)
            return;

        if (data.GetTagItem("enduser.id") is not null)
            return;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
            data.SetTag("enduser.id", userId);
    }
}
