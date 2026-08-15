// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests.Auth;

internal sealed class JwtTokenBuilder
{
    private string _subject = string.Empty;
    private readonly List<string> _scopes = [];

    public JwtTokenBuilder AddSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public JwtTokenBuilder AddScope(string scope)
    {
        _scopes.Add(scope);
        return this;
    }

    public string Build()
    {
        var token = new JwtSecurityToken(
            issuer: "https://local.dev/",
            audience: "API",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, _subject) }
                .Append(new Claim("scope", string.Join(' ', _scopes))),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                    "IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLong")),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
