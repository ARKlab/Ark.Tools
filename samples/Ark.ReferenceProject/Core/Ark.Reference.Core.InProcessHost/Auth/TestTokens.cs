// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Reference.Core.Common.Auth;

using Microsoft.IdentityModel.Tokens;

namespace Ark.Reference.Core.InProcessHost.Auth;

/// <summary>
/// Creates tokens accepted by the API in the IntegrationTests environment.
/// </summary>
public static class TestTokens
{
    /// <summary>
    /// Creates a token builder signed, addressed and issued for the IntegrationTests environment.
    /// </summary>
    /// <returns>A builder with a 60-minute expiry.</returns>
    public static JwtTokenBuilder CreateBuilder()
    {
        return new JwtTokenBuilder()
            .AddSecurityKey(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey)))
            .AddSubject("TestSubject")
            .AddAudience(AuthConstants.IntegrationTestsAudience)
            .AddIssuer($"https://{AuthConstants.IntegrationTestsDomain}/")
            .AddExpiry(60);
    }
}
