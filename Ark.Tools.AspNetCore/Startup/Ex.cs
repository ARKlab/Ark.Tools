// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.Swashbuckle;

using Asp.Versioning;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
        internal static IServiceCollection ArkConfigureSwaggerVersions(this IServiceCollection services, IEnumerable<ApiVersion> versions, Func<ApiVersion, OpenApiInfo> infoBuilder)
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
			var authScheme = "Auth0";

			var defaultPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(authScheme)
                .RequireAuthenticatedUser()
                .Build();

            services.AddMvcCore()
                .AddMvcOptions(opt =>
                {
                    opt.Filters.Add(new AuthorizeFilter(defaultPolicy));
                });

			services.AddAuthentication(options =>
				{
					options.DefaultAuthenticateScheme = authScheme;
					options.DefaultChallengeScheme = authScheme;

				})
				.AddJwtBearerArkDefault(authScheme, audience, domain)
				;

            //.AddJwtBearer("Auth0", o =>
            //{
            //    o.Audience = audience;
            //    o.Authority = $"https://{domain}/";
            //    o.TokenValidationParameters = new TokenValidationParameters
            //    {
            //        NameClaimTypeRetriever = (a, b) =>
            //        {
            //            if (a is JwtSecurityToken jwt)
            //            {
            //                if (jwt.Claims.Any(x => x.Type == "http://ark-energy.eu/claims/email"))
            //                {
            //                    return "http://ark-energy.eu/claims/email";
            //                }
            //                else if (jwt.Claims.Any(x => x.Type == ClaimTypes.Email))
            //                {
            //                    return ClaimTypes.Email;
            //                }
            //                else if (jwt.Claims.Any(x => x.Type == "appid") && jwt.Claims.Where(w => w.Type == "appidacr").SingleOrDefault()?.Value == "1")
            //                {
            //                    return "appid";
            //                }
            //            }

            //            return ClaimTypes.NameIdentifier;
            //        }
            //    };
            //})
            //;

            services.ArkConfigureSwaggerAuth0(domain, audience, swaggerClientId);

            return services;
        }


		public static AuthenticationBuilder AddJwtBearerArkDefault(
						this AuthenticationBuilder builder
					  , string authenticationScheme
			          , string audience
					  , string domain
					  , Action<JwtBearerOptions>? configureOptions = null)
		{
			return  builder.AddJwtBearer(authenticationScheme, o =>
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
				configureOptions?.Invoke(o);
			});
		}

		public static IServiceCollection ArkConfigureSwaggerAuth0(this IServiceCollection services, string domain, string audience, string clientId)
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
							AuthorizationUrl = new Uri($"https://{domain}/authorize"),
							Scopes = new Dictionary<string, string>
							{
								{ "openid", "Grant access to user" }
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
                    { "audience", audience }
                });
            });

            return services;
        }

        public static IServiceCollection ArkConfigureSwaggerAzureB2C(this IServiceCollection services, string instance, string domain, string clientId, string signUpSignIn, string apiId)
        {
            services.ConfigureSwaggerGen(c =>
            {
                var oauthScheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    
                    Flows = new OpenApiOAuthFlows()
                    {
                        AuthorizationCode = new OpenApiOAuthFlow()
                        {
                            AuthorizationUrl = new Uri($"{instance}/{domain}/{signUpSignIn}/oauth2/v2.0/authorize"),
                            TokenUrl = new Uri($"{instance}/{domain}/{signUpSignIn}/oauth2/v2.0/token"),                            
                            Scopes = new Dictionary<string, string>
                            {
                                { "openid", "Grant access to user" },
                                { $"https://{domain}/{apiId}/access_as_user", "Default scope to retrieve user permissions" }
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
    }
}
