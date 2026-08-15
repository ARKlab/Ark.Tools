// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.WebInterface.Auth;

/// <summary>Authentication scheme and integration-test settings.</summary>
public static class AuthConstants
{
    /// <summary>Gets the Entra ID authentication scheme name.</summary>
    public const string AzureAdSchema = "AzureAd";

    /// <summary>Gets the Azure AD B2C authentication scheme name.</summary>
    public const string AzureAdB2CSchema = "AzureAdB2C";

    /// <summary>Gets the Azure AD B2C configuration section.</summary>
    public const string AzureB2CConfigSection = "AzureAdB2C";

    /// <summary>Gets the integration-test audience.</summary>
    public const string IntegrationTestsAudience = "API";

    /// <summary>Gets the integration-test issuer.</summary>
    public const string IntegrationTestsDomain = "local.dev";

    /// <summary>Gets the integration-test signing key.</summary>
    public const string IntegrationTestsEncryptionKey = "IntegrationTestsSecretVeryLongForH256VeryLongVeryLongVeryLongVeryLongVeryLong";

    /// <summary>Gets the role claim type.</summary>
    public const string ClaimRole = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
}
