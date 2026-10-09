// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Web.Hosting;

/// <summary>Exposes the principal restored from the current message to the application.</summary>
public sealed class MessagingPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
{
    // Instance field, not static: each participant (and each test participant in one process)
    // owns its ambient principal, so nothing leaks between providers.
    private readonly AsyncLocal<ClaimsPrincipal?> _current = new();

    /// <inheritdoc />
    public ClaimsPrincipal Current => _current.Value ?? new ClaimsPrincipal(new ClaimsIdentity());

    /// <summary>Sets the principal for the current message flow.</summary>
    /// <param name="principal">The restored principal.</param>
    public void Set(ClaimsPrincipal principal)
    {
        _current.Value = principal;
    }
}
