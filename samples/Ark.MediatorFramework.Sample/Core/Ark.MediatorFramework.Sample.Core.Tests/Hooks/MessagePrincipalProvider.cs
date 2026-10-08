// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>Exposes the principal restored from the current message to one participant's application.</summary>
public sealed class MessagePrincipalProvider : IContextProvider<ClaimsPrincipal>
{
    // Instance field, not static: each test participant owns its ambient principal.
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
