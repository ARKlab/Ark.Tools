// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

internal sealed class TestPrincipalProvider : IContextProvider<ClaimsPrincipal>
{
    public ClaimsPrincipal Current { get; } = new(
        new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "hosting-test-user"),
            new Claim("scope", "hosting.test"),
        ],
        authenticationType: "hosting-test"));
}
