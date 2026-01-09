using Ark.Reference.Core.Common.Auth;
using Ark.Tools.AspNetCore.Swashbuckle;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ark.Reference.Core.WebInterface.Utils;

public static class Ex
{
    public static TokenValidationParameters TokenValidator() => new()
    {
        NameClaimTypeRetriever = (a, b) =>
        {
            if (a is JwtSecurityToken jwt)
            {
                if (jwt.Claims.Any(x => x.Type == "http://ark-energy.eu/claims/email"))
                {
                    return "http://ark-energy.eu/claims/email";
                }
                else if (jwt.Claims.Any(x => x.Type == ClaimTypes.Email))
                {
                    return ClaimTypes.Email;
                }
                else if (jwt.Claims.Any(x => x.Type == "email"))
                {
                    return ClaimTypes.Email;
                }
                else if (jwt.Claims.Any(x => x.Type == "upn"))
                {
                    return ClaimTypes.Upn;
                }
                else if (jwt.Claims.Any(x => x.Type == "emails"))
                {
                    return "emails";
                }
                else if (jwt.Claims.Any(x => x.Type == "appid") && jwt.Claims.SingleOrDefault(w => w.Type == "appidacr")?.Value == "1")
                {
                    return "appid";
                }
            }

            return ClaimTypes.NameIdentifier;
        }
    };

    public static void ConfigureAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddAuthentication(options =>
        {
            options.DefaultScheme = "smart";
        })
        .AddPolicyScheme("smart", "smart", o =>
        {
            o.ForwardDefaultSelector = ctx =>
            {
                string? authorization = ctx.Request.Headers.Authorization;

                if (String.IsNullOrWhiteSpace(authorization))
                    return null;

                if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return null;

                var token = authorization["Bearer ".Length..].Trim();
                if (string.IsNullOrEmpty(token))
                    return null;

                var decoded = new JwtSecurityToken(token);

                if (decoded.Issuer.StartsWith("https://login.microsoftonline.com/", StringComparison.Ordinal))
                    return AuthConstants.AzureAdSchema;
                else
                    return AuthConstants.AzureAdB2CSchema;
            };

            o.ForwardDefault = AuthConstants.AzureAdB2CSchema;
        })
        .AddJwtBearer(AuthConstants.AzureAdSchema, o =>
        {
            var tenantId = configuration.GetSection(AuthConstants.AzureB2CConfigSection)["TenantId"];
            var audience = configuration.GetSection(AuthConstants.AzureB2CConfigSection)["ClientId"];

            o.TokenValidationParameters = TokenValidator();

            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "IntegrationTests")
            {
#pragma warning disable CA5404 // Do not disable token validation checks
                o.TokenValidationParameters.ValidateAudience = false;
                o.TokenValidationParameters.ValidateIssuer = false;
#pragma warning restore CA5404 // Do not disable token validation checks
                o.TokenValidationParameters.ValidateIssuerSigningKey = false;
                o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey));
            }
            else
            {
                o.Audience = audience;
                o.Authority = $@"https://login.microsoftonline.com/{tenantId}/v2.0";

                o.TokenValidationParameters.ValidAudience = audience;
                o.TokenValidationParameters.ValidIssuer = $@"https://login.microsoftonline.com/{tenantId}/v2.0";
            }
        })
        .AddMicrosoftIdentityWebApi(o =>
        {
            configuration.Bind(AuthConstants.AzureB2CConfigSection, o);

            o.TokenValidationParameters = TokenValidator();

            o.TokenValidationParameters.NameClaimType = "name";
            // NOTE: SecurityTokenValidators is obsolete in newer versions, but required for custom claim mapping.
            // Modern alternative would use MapInboundClaims property on JwtSecurityTokenHandler,
            // but this requires different initialization pattern. Keeping current approach for backward compatibility.
#pragma warning disable CS0618 // Type or member is obsolete
            (o.SecurityTokenValidators[0] as JwtSecurityTokenHandler)?.InboundClaimTypeMap.Add("extension_permissions", "scope");
#pragma warning restore CS0618 // Type or member is obsolete

            o.Events = new JwtBearerEvents
            {
                //OnTokenValidated = ctx => CommonEx.AddAdminClaim(ctx)
            };
        },
        options =>
        {
            configuration.Bind(AuthConstants.AzureB2CConfigSection, options);
        }, AuthConstants.AzureAdB2CSchema);
    }

    public static IServiceCollection ArkConfigureSwaggerEntraId(this IServiceCollection services, string instance, string domain, string clientId, string tenantId)
    {
        services.ConfigureSwaggerGen(c =>
        {
            var oauthScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,

                Flows = new OpenApiOAuthFlows()
                {
                    Implicit = new OpenApiOAuthFlow()
                    {
                        AuthorizationUrl = new Uri($"{instance}/{tenantId}/oauth2/v2.0/authorize"),
                        TokenUrl = new Uri($"{instance}/{tenantId}/oauth2/v2.0/token"),
                        Scopes = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            { "openid", "Grant access to user" },
                            { $"api://{clientId}/access_as_user", "Default scope to retrieve user permissions" }
                        }
                    }
                },
                Scheme = "oauth2"
            };
            c.AddSecurityDefinition("oauth2", oauthScheme);
            c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
            {
                [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
            });
        });

        services.ArkConfigureSwaggerUI(c =>
        {
            c.OAuthClientId(clientId);
            c.OAuthAppName("WebApi");
            c.OAuthUsePkce();
        });

        return services;
    }


    public static IServiceCollection ArkConfigureSwaggerIdentityServer(this IServiceCollection services, string domain, string clientId, string swaggerscope)
    {
        services.ConfigureSwaggerGen(c =>
        {
            var oauthScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows()
                {
                    Implicit = new OpenApiOAuthFlow()
                    {
                        AuthorizationUrl = new Uri($"https://{domain}/connect/authorize"),
                        Scopes = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            { swaggerscope, "Grant access to user" }
                        }
                    }
                },
                Scheme = "oauth2"
            };

            c.AddSecurityDefinition("oauth2", oauthScheme);

            c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
            {
                [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
            });
        });

        services.ArkConfigureSwaggerUI(c =>
        {
            c.OAuthClientId(clientId);
            c.OAuthAppName("WebApi");
            c.OAuthAdditionalQueryStringParams(new Dictionary<string, string>(StringComparer.Ordinal)
            {
            });
        });

        return services;
    }
}