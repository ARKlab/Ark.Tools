// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.Swashbuckle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace Ark.Tools.AspNetCore.Startup
{
    public static class Ex
    {
        internal static IServiceCollection ArkConfigureSwaggerVersions(this IServiceCollection services, IEnumerable<ApiVersion> versions, Func<ApiVersion, Info> infoBuilder)
        {
            services.ConfigureSwaggerGen(c =>
            {
                foreach (var v in versions)
                    c.SwaggerDoc($"v{v.ToString("VVVV")}", infoBuilder(v));
            });

            services.ArkConfigureSwaggerUI(c =>
            {
                foreach (var v in versions)
                    c.SwaggerEndpoint($@"docs/v{v.ToString("VVVV")}", $@"v{v.ToString("VVVV")} Docs");
            });

            return services;
        }

        public static IServiceCollection ArkConfigureAuth0(this IServiceCollection services, string domain, string audience, string swaggerClientId)
        {
            var defaultPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes("Auth0")
                .RequireAuthenticatedUser()
                .Build();

            services.AddMvcCore()
                .AddMvcOptions(opt =>
                {
                    opt.Filters.Add(new AuthorizeFilter(defaultPolicy));
                });

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Auth0";
                options.DefaultChallengeScheme = "Auth0";

            })
            .AddJwtBearer("Auth0", o =>
            {
                o.Audience = audience;
                o.Authority = $"https://{domain}/";
                o.TokenValidationParameters = new TokenValidationParameters
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
                            else if (jwt.Claims.Any(x => x.Type == "appid") && jwt.Claims.Where(w => w.Type == "appidacr").SingleOrDefault()?.Value == "1")
                            {
                                return "appid";
                            }
                        }

                        return ClaimTypes.NameIdentifier;
                    }
                }; 
            });
            ;

            services.ArkConfigureSwaggerAuth0(domain, audience, swaggerClientId);

            return services;
        }

        public static IServiceCollection ArkConfigureSwaggerAuth0(this IServiceCollection services, string domain, string audience, string clientId)
        {
            services.ConfigureSwaggerGen(c =>
            {

                var oauthScheme = new OAuth2Scheme
                {
                    Type = "oauth2",
                    Flow = "implicit",
                    AuthorizationUrl = $"https://{domain}/authorize",
                    Scopes = new Dictionary<string, string>
                        {
                            { "openid profile email", "Grant access to user" }
                        }
                };
                c.AddSecurityDefinition("oauth2", oauthScheme);
            });

            services.ArkConfigureSwaggerUI(c =>
            {
                c.OAuthClientId(clientId);
                c.OAuthAppName("WebApi");
                c.OAuthAdditionalQueryStringParams(new Dictionary<string, string>()
                {
                    { "audience", audience }
                });
            });

            return services;
        }
    }
}
