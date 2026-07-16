// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using Reqnroll;

using System.Net.Http.Headers;

namespace Ark.MediatorFramework.Sample.Tests.Auth;

/// <summary>Controls the bearer token used by authentication scenarios.</summary>
[Binding]
public sealed class AuthTestContext
{
    private readonly SampleTestContext _context;
    private readonly JwtTokenBuilder _tokenBuilder = new();

    /// <summary>Gets the token used for authenticated requests.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Configures the integration-test authentication environment.</summary>
    [BeforeTestRun(Order = 0)]
    public static void ConfigureEnvironment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
    }

    /// <summary>Initializes a new instance of the <see cref="AuthTestContext"/> class.</summary>
    public AuthTestContext(SampleTestContext context)
    {
        _context = context;
        SetAuthenticatedUser();
    }

    /// <summary>Sets the request client to use a valid integration-test token.</summary>
    [Given("I am an authenticated user")]
    [BeforeScenario]
    public void SetAuthenticatedUser()
    {
        Token = _tokenBuilder.AddSubject("test-user").AddGreetingWriteScope().Build();
        _context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
    }

    /// <summary>Sets the request client to make anonymous requests.</summary>
    [Given("I am an anonymous user")]
    public void SetAnonymousUser()
    {
        _context.Client.DefaultRequestHeaders.Authorization = null;
    }
}
