using Ark.Tools.AspNetCore.Swashbuckle;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using System.Linq;
using Core.Service.Common.Auth;

namespace Core.Service.WebInterface.Utils
{
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
                    else if (jwt.Claims.Any(x => x.Type == "appid") && jwt.Claims.Where(w => w.Type == "appidacr").SingleOrDefault()?.Value == "1")
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
                    string authorization = ctx.Request.Headers["Authorization"];

                    if (String.IsNullOrWhiteSpace(authorization))
                        return null;

                    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        return null;

                    var token = authorization.Substring("Bearer ".Length).Trim();
                    if (string.IsNullOrEmpty(token))
                        return null;

                    var decoded = new JwtSecurityToken(token);

                    if (decoded.Issuer.StartsWith("https://login.microsoftonline.com/"))
                        return AuthConstants.AzureAdSchema;
                    else
                        return AuthConstants.AzureAdB2CSchema;
                };

                o.ForwardDefault = AuthConstants.AzureAdB2CSchema;
            })
            .AddJwtBearer(AuthConstants.AzureAdSchema, o =>
            {
                var tenantId = configuration.GetSection(AuthConstants.AzureB2CSection)["TenantId"];
                var audience = configuration.GetSection(AuthConstants.AzureB2CSection)["ClientId"];

                o.TokenValidationParameters = TokenValidator();

                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "SpecFlow")
                {
                    o.TokenValidationParameters.ValidateAudience = false;
                    o.TokenValidationParameters.ValidateIssuer = false;
                    o.TokenValidationParameters.ValidateIssuerSigningKey = false;
                    o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.SpecFlowEncryptionKey));
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
                configuration.Bind(AuthConstants.AzureB2CSection, o);

                o.TokenValidationParameters = TokenValidator();

                o.TokenValidationParameters.NameClaimType = "name";
#pragma warning disable CS0618 // Type or member is obsolete //TODO
                (o.SecurityTokenValidators[0] as JwtSecurityTokenHandler)?.InboundClaimTypeMap.Add("extension_permissions", "scope");
#pragma warning restore CS0618 // Type or member is obsolete

                o.Events = new JwtBearerEvents
                {
                    //OnTokenValidated = ctx => CommonEx.AddAdminClaim(ctx)
                };
            },
            options =>
            {
                configuration.Bind(AuthConstants.AzureB2CSection, options);
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
                            Scopes = new Dictionary<string, string>
                            {
                                { "openid", "Grant access to user" },
                                { $"api://{clientId}/access_as_user", "Default scope to retrieve user permissions" }
                            }
                        }
                    },

                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "oauth2"
                    },
                    Scheme = "oauth2"
                };
                c.AddSecurityDefinition("oauth2", oauthScheme);
                c.OperationFilter<SecurityRequirementsOperationFilter>();
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
                            Scopes = new Dictionary<string, string>
                            {
                                { swaggerscope, "Grant access to user" }
                            }
                        }
                    },
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "oauth2"
                    },
                    Scheme = "oauth2"
                };

                c.AddSecurityDefinition("oauth2", oauthScheme);

                c.OperationFilter<SecurityRequirementsOperationFilter>();
            });

            services.ArkConfigureSwaggerUI(c =>
            {
                c.OAuthClientId(clientId);
                c.OAuthAppName("WebApi");
                c.OAuthAdditionalQueryStringParams(new Dictionary<string, string>()
                {
                });
            });

            return services;
        }
    }
}
