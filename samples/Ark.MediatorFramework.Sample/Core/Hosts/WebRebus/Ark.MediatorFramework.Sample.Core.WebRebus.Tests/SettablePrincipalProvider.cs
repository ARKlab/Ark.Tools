// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Reflection;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>The api test process user: authenticated, with scopes the test can change.</summary>
internal sealed class SettablePrincipalProvider : IContextProvider<ClaimsPrincipal>
{
    private static readonly string[] _allScopes = typeof(ApplicationScopes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (string)field.GetRawConstantValue()!)
        .ToArray();

    private volatile ClaimsPrincipal _current = _create(_allScopes);

    /// <inheritdoc />
    public ClaimsPrincipal Current => _current;

    /// <summary>Replaces the user's scopes.</summary>
    /// <param name="scopes">The granted scopes.</param>
    public void Set(params string[] scopes)
    {
        _current = _create(scopes);
    }

    private static ClaimsPrincipal _create(string[] scopes)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "rebus-test-user"),
                new Claim("scope", string.Join(' ', scopes)),
            ],
            "IntegrationTests"));
    }
}
