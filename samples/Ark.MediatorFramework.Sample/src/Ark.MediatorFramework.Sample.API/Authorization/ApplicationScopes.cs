// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;

namespace Ark.MediatorFramework.Sample.API.Authorization;

/// <summary>Names the scopes used by the sample API.</summary>
public static class ApplicationScopes
{
    /// <summary>Allows reading books.</summary>
    public const string BookRead = "books.read";

    /// <summary>Allows creating, updating, and deleting books.</summary>
    public const string BookWrite = "books.write";

    /// <summary>Allows reading and writing book covers.</summary>
    public const string BookCover = "books.cover";

    /// <summary>Allows reading book reviews and reading activity.</summary>
    public const string BookReviewsRead = "books.reviews.read";

    /// <summary>Allows writing book reviews.</summary>
    public const string BookReviewsWrite = "books.reviews.write";

    /// <summary>Allows reading book reading activity.</summary>
    public const string BookActivityRead = "books.activity.read";

    /// <summary>Allows recording book reading activity.</summary>
    public const string BookActivityWrite = "books.activity.write";
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
