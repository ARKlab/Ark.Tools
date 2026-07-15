// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Rebus;
using Ark.Tools.Solid;


using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>
/// Hosting-level <see cref="IContextProvider{T}"/> unifying the two transport-specific principals
/// for the single-container sample: it reads <see cref="HttpContext.User"/> (the AspNetCore
/// authentication result, exactly as <c>AspNetCoreUserContextProvider</c> does) when serving an HTTP
/// request, and otherwise falls back to the Rebus-propagated principal
/// (<see cref="RebusPrincipalContextWithFallbackProvider"/>, fed by <c>UserFlowStep</c>).
/// </summary>
/// <remarks>
/// ponytail: a single container serving both HTTP and Rebus is a sample simplification so the
/// transport-parity test can share one store. Production hosts the Rebus processor in its own
/// container (see the ReferenceProject), each with its own <see cref="IContextProvider{T}"/>.
/// </remarks>
public sealed class HostUserContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IHttpContextAccessor _http;
    private readonly RebusPrincipalContextWithFallbackProvider _rebus;

    /// <summary>Initializes a new instance of the <see cref="HostUserContextProvider"/> class.</summary>
    public HostUserContextProvider(IHttpContextAccessor http, IMessageContextProvider messageContextProvider)
    {
        _http = http;
        _rebus = new RebusPrincipalContextWithFallbackProvider(messageContextProvider);
    }

    /// <inheritdoc />
    public ClaimsPrincipal Current
        => _http.HttpContext?.User ?? _rebus.Current;
}
