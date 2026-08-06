// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

internal sealed class TestPrincipalProvider : IContextProvider<ClaimsPrincipal>
{
    private ClaimsPrincipal _current = new(new ClaimsIdentity());

    public ClaimsPrincipal Current => _current;

    internal void SetCurrent(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        _current = principal;
    }
}
