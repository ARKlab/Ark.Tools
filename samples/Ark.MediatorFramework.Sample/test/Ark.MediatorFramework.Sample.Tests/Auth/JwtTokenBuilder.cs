// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Tests.Auth;

internal sealed class JwtTokenBuilder
{
    private string _subject = string.Empty;
    private bool _greetingWriteScope;

    public JwtTokenBuilder AddSubject(string subject)
    {
        _subject = subject;
        return this;
    }

    public JwtTokenBuilder AddGreetingWriteScope()
    {
        _greetingWriteScope = true;
        return this;
    }

    public string Build()
    {
        var token = new JwtSecurityToken(
            issuer: "https://local.dev/",
            audience: "API",
            claims: _greetingWriteScope
                ? [new Claim(JwtRegisteredClaimNames.Sub, _subject), new Claim("scope", "greetings.write")]
                : [new Claim(JwtRegisteredClaimNames.Sub, _subject)],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                    "IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLong")),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
