// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;
using Ark.Tools.Authorization.Requirement;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Requires the greeting-write scope for the shared greeting mutation.</summary>
public sealed class GreetingAuthorizationPolicy : AuthorizationPolicy
{
    /// <inheritdoc />
    protected override void Build(AuthorizationPolicyBuilder builder)
    {
        builder.RequireClaim("scope", "greetings.write");
    }
}

/// <summary>Evaluates claim requirements used by the sample's greeting policy.</summary>
public sealed class GreetingAuthorizationHandler : IAuthorizationHandler
{
    /// <inheritdoc />
    public Task HandleAsync(AuthorizationContext context, CancellationToken ctk = default)
    {
        foreach (var requirement in context.Policy.Requirements.OfType<ClaimsAuthorizationRequirement>())
        {
            if (context.User.Claims.Any(claim =>
                string.Equals(claim.Type, requirement.ClaimType, StringComparison.OrdinalIgnoreCase)
                && (requirement.AllowedValues is null
                    || requirement.AllowedValues.Contains(claim.Value, StringComparer.Ordinal))))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
