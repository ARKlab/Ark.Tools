// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Authorization;

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
