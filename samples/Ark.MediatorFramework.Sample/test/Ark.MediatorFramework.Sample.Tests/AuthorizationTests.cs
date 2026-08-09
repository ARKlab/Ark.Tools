// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.Tools.Authorization;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies application policy and user-context behavior.</summary>
[TestClass]
public sealed class AuthorizationTests
{
    /// <summary>Allows a principal with the greeting-write scope to create a greeting.</summary>
    [TestMethod]
    public async Task AuthorizedDispatchUsesUserContext()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("authorized-user", ApplicationScopes.GreetingWrite);

        var response = await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest { Name = "authorized" }).ConfigureAwait(false);

        response.Message.Should().Contain("authorized-user");
    }

    /// <summary>Rejects a principal without the required policy scope.</summary>
    [TestMethod]
    public async Task MissingGreetingWriteScopeThrowsAuthorizationException()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetPrincipal(new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier,
                    "unauthorized-user")],
                authenticationType: "application-test")));

        var action = () => context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest { Name = "unauthorized" });

        await action.Should().ThrowAsync<PolicyAuthorizationException>().ConfigureAwait(false);
    }
}
