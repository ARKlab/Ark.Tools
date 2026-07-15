// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.WebInterface.Auth;

/// <summary>Authentication configuration extensions matching the ReferenceProject host.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix matches the ReferenceProject extension convention.")]
public static class AuthenticationEx
{
    /// <summary>Registers the smart Entra ID and Azure AD B2C bearer authentication schemes.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "IntegrationTests")
        {
            _ = services.AddAuthentication(options => options.DefaultScheme = "IntegrationTests")
                .AddJwtBearer("IntegrationTests", options =>
                {
                    options.Audience = AuthConstants.IntegrationTestsAudience;
                    options.TokenValidationParameters = TokenValidator();
#pragma warning disable CA5404
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey));
#pragma warning restore CA5404
                });
            return;
        }

        _ = services.AddAuthentication(options => options.DefaultScheme = "smart")
            .AddPolicyScheme("smart", "smart", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return null;

                    var token = authorization["Bearer ".Length..].Trim();
                    if (token.Length == 0)
                        return null;

                    var decoded = new JwtSecurityToken(token);
                    return decoded.Issuer.StartsWith("https://login.microsoftonline.com/", StringComparison.Ordinal)
                        ? AuthConstants.AzureAdSchema
                        : AuthConstants.AzureAdB2CSchema;
                };
                options.ForwardDefault = AuthConstants.AzureAdB2CSchema;
            })
            .AddJwtBearer(AuthConstants.AzureAdSchema, options =>
            {
                var section = configuration.GetSection(AuthConstants.AzureB2CConfigSection);
                var tenantId = section["TenantId"];
                var audience = section["ClientId"];
                options.TokenValidationParameters = TokenValidator();

                options.Audience = audience;
                options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                options.TokenValidationParameters.ValidAudience = audience;
                options.TokenValidationParameters.ValidIssuer = options.Authority;
            })
            .AddMicrosoftIdentityWebApi(options =>
            {
                configuration.Bind(AuthConstants.AzureB2CConfigSection, options);
                options.TokenValidationParameters = TokenValidator();
                options.TokenValidationParameters.NameClaimType = "name";
            },
            options => configuration.Bind(AuthConstants.AzureB2CConfigSection, options),
            AuthConstants.AzureAdB2CSchema);
    }

    private static TokenValidationParameters TokenValidator()
    {
        return new TokenValidationParameters
        {
            NameClaimTypeRetriever = (token, _) =>
            {
                if (token is JwtSecurityToken jwt)
                {
                    if (jwt.Claims.Any(claim => claim.Type == "http://ark-energy.eu/claims/email"))
                        return "http://ark-energy.eu/claims/email";
                    if (jwt.Claims.Any(claim => claim.Type == ClaimTypes.Email || claim.Type == "email"))
                        return ClaimTypes.Email;
                    if (jwt.Claims.Any(claim => claim.Type == "upn"))
                        return ClaimTypes.Upn;
                    if (jwt.Claims.Any(claim => claim.Type == "emails"))
                        return "emails";
                    if (jwt.Claims.Any(claim => claim.Type == "appid")
                        && jwt.Claims.SingleOrDefault(claim => claim.Type == "appidacr")?.Value == "1")
                        return "appid";
                }

                return ClaimTypes.NameIdentifier;
            },
        };
    }
}
