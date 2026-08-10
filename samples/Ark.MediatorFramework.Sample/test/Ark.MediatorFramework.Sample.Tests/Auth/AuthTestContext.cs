// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Reqnroll;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests.Auth;

/// <summary>Controls the principal used by application authorization scenarios.</summary>
[Binding]
public sealed class AuthTestContext
{
    private readonly SampleTestContext _context;

    /// <summary>Initializes a new instance of the <see cref="AuthTestContext"/> class.</summary>
    public AuthTestContext(SampleTestContext context)
    {
        _context = context;
    }

    /// <summary>Sets the application principal to an authenticated user with the write scope.</summary>
    [Given("I am an authenticated user")]
    public void SetAuthenticatedUser()
    {
        _context.Application.SetAuthenticatedUser("test-user", ApplicationScopes.GreetingWrite);
    }

    /// <summary>Sets the application principal to the requested authenticated subject.</summary>
    /// <param name="subject">The authenticated subject.</param>
    [Given(@"I am an authenticated user named ""(.*)""")]
    public void SetAuthenticatedUser(string subject)
    {
        _context.Application.SetAuthenticatedUser(subject, ApplicationScopes.GreetingWrite);
    }

    /// <summary>Sets an authenticated principal without the greeting-write scope.</summary>
    [Given("I am an authenticated user without the greeting write scope")]
    public void SetAuthenticatedUserWithoutGreetingWriteScope()
    {
        _context.Application.SetAuthenticatedUser("unauthorized-user", "other-scope");
    }

    /// <summary>Sets the application principal to an anonymous user.</summary>
    [Given("I am an anonymous user")]
    public void SetAnonymousUser()
    {
        _context.Application.SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));
    }
}
