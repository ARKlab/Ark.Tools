// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.AzureFunctions.Tests;

/// <summary>
/// Issues symmetric-key JWTs accepted by the sample host's IntegrationTests authentication scheme.
/// </summary>
internal static class JwtTokenBuilder
{
    /// <summary>Builds a signed bearer token for the given subject and scopes.</summary>
    /// <param name="subject">The token subject.</param>
    /// <param name="scopes">The granted scopes.</param>
    /// <returns>The serialized JWT.</returns>
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
