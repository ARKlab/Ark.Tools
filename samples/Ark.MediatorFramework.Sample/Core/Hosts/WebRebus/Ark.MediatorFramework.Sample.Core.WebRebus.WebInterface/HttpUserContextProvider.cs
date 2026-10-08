// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface;

/// <summary>
/// Exposes the authenticated <see cref="HttpContext.User"/> to the application, as
/// <c>Ark.Tools.AspNetCore.AspNetCoreUserContextProvider</c> does.
/// </summary>
/// <remarks>
/// ponytail: a local copy because that provider ships in <c>Ark.Tools.AspNetCore</c>, which would add
/// OData, versioning, and Swashbuckle dependencies to this host for one property.
/// </remarks>
public sealed class HttpUserContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IHttpContextAccessor _http;

    /// <summary>Initializes a new instance of the <see cref="HttpUserContextProvider"/> class.</summary>
    /// <param name="http">The HTTP context accessor forwarded from Microsoft DI.</param>
    public HttpUserContextProvider(IHttpContextAccessor http)
    {
        _http = http;
    }

    /// <inheritdoc />
    public ClaimsPrincipal Current => _http.HttpContext?.User
        ?? throw new InvalidOperationException("The current user is available only while serving a request.");
}
