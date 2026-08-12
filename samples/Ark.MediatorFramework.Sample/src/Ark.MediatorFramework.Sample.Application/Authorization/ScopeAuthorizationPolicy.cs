// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;

namespace Ark.MediatorFramework.Sample.Application.Authorization;

/// <summary>Evaluates scope requirements against the current user's claims.</summary>
public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeAuthorizationRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationContext context,
        ScopeAuthorizationRequirement requirement,
        CancellationToken ctk = default)
    {
        if (context.User.Claims.Any(claim =>
            string.Equals(claim.Type, "scope", StringComparison.OrdinalIgnoreCase)
            && claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(requirement.Scope, StringComparer.Ordinal)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
