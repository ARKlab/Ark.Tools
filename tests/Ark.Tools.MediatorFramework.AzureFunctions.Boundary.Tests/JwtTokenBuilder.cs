// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.AzureFunctions.Boundary.Tests;

internal static class JwtTokenBuilder
{
    public static string Build(string subject, params string[] scopes)
    {
        var token = new JwtSecurityToken(
            issuer: "https://local.dev/",
            audience: "API",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) }
                .Append(new Claim("scope", string.Join(' ', scopes))),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                    "IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLong")),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
