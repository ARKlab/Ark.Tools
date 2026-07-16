// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Names the scopes used by the sample application.</summary>
public static class ApplicationScopes
{
    /// <summary>Allows creating greetings.</summary>
    public const string GreetingWrite = "greetings.write";
}

/// <summary>Requires a scope claim.</summary>
public sealed class RequireScopePolicy : IAuthorizationPolicy
{
    /// <summary>Initializes a new instance of the <see cref="RequireScopePolicy"/> class.</summary>
    /// <param name="scope">The required scope.</param>
    public RequireScopePolicy(string scope)
    {
        Scope = scope;
        var builder = new AuthorizationPolicyBuilder(nameof(RequireScopePolicy));
        builder.AddRequirements(new ScopeAuthorizationRequirement(Scope));
        var policy = builder.Build();
        Name = policy.Name;
        Requirements = policy.Requirements;
    }

    /// <summary>Gets the required scope.</summary>
    public string Scope { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<IAuthorizationRequirement> Requirements { get; }
}

/// <summary>Creates a policy requiring the specified scope.</summary>
public sealed class RequireScopePolicyAttribute : PolicyAuthorizeAttribute
{
    /// <summary>Initializes a new instance of the <see cref="RequireScopePolicyAttribute"/> class.</summary>
    /// <param name="scope">The required scope.</param>
    public RequireScopePolicyAttribute(string scope)
        : base(typeof(RequireScopePolicy), scope)
    {
    }
}

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

/// <summary>Represents a required application scope.</summary>
public sealed class ScopeAuthorizationRequirement : IAuthorizationRequirement
{
    /// <summary>Initializes a new instance of the <see cref="ScopeAuthorizationRequirement"/> class.</summary>
    /// <param name="scope">The required scope.</param>
    public ScopeAuthorizationRequirement(string scope)
    {
        Scope = scope;
    }

    /// <summary>Gets the required scope.</summary>
    public string Scope { get; }
}
