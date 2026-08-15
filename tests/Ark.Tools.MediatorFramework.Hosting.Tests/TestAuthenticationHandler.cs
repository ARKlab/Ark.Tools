// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    internal const string _schemeName = "HostingTest";

    private readonly TestPrincipalProvider _principalProvider;

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestPrincipalProvider principalProvider)
        : base(options, logger, encoder)
    {
        _principalProvider = principalProvider;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            _principalProvider.SetCurrent(anonymous);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization["Bearer ".Length..]
            : string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "hosting-test-user"),
        };
        if (string.Equals(token, "scope", StringComparison.Ordinal))
            claims.Add(new Claim("scope", "hosting.test"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, _schemeName));
        _principalProvider.SetCurrent(principal);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, _schemeName)));
    }
}
