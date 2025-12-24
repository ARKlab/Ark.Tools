using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;

using Asp.Versioning;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.OpenApi;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;

using TestWithoutArkTools.Application.Host;

namespace TestWithoutArkTools
{
    internal sealed class Startup : ArkStartupWebApi
    {
        public Startup(IConfiguration configuration, IHostEnvironment env)
            : base(configuration, env)
        {
        }

        public override IEnumerable<ApiVersion> Versions => [new ApiVersion(1, 0)];

        public override OpenApiInfo MakeInfo(ApiVersion version)
            => new()
            {
                Title = "API",
                Version = version.ToString("VVVV", CultureInfo.InvariantCulture),
            };

        // This method gets called by the runtime. Use this method to add services to the container.
        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            var auth0Scheme = "Auth0";
            var audience = "Audience";
            var domain = "Domain";
            var swaggerClientId = "SwaggerClientId";

            var defaultPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(auth0Scheme)
                .RequireAuthenticatedUser()
                .Build();

            var authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = auth0Scheme;
                options.DefaultChallengeScheme = auth0Scheme;

            })
            .AddJwtBearerArkDefault(auth0Scheme, audience, domain, o =>
            {
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "IntegrationTests")
                {
                    o.TokenValidationParameters.ValidIssuer = o.Authority;
                    o.Authority = null;
                    //o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.ClientSecretIntegrationTests));
                }
                o.TokenValidationParameters.RoleClaimType = "Role";
            })
            ;

            bool isAuth0 = String.IsNullOrWhiteSpace(Configuration["AzureAdB2C:Domain"]) ? true : false;

            if (!isAuth0)
            {
                authBuilder.AddMicrosoftIdentityWebApi(options =>
                {
                    Configuration.Bind("AzureAdB2C", options);

                    options.TokenValidationParameters.NameClaimType = "name";
                    (options.TokenHandlers[0] as JwtSecurityTokenHandler)?.InboundClaimTypeMap.Add("extension_Scope", "scope");
                },
                    options =>
                    {
                        Configuration.Bind("AzureAdB2C", options);
                    }, JwtBearerDefaults.AuthenticationScheme);

                services.ArkConfigureSwaggerAzureB2C(Configuration["AzureAdB2C:Instance"], Configuration["AzureAdB2C:Domain"], Configuration["AzureAdB2C:ClientId"], Configuration["AzureAdB2C:SignUpSignInPolicyId"], Configuration["AzureAdB2C:ApiId"]);
            }
            else
            {
                services.ArkConfigureSwaggerAuth0(domain, audience, swaggerClientId);
            }




            services.ArkConfigureSwaggerUI(c =>
            {
            });

            services.ConfigureSwaggerGen(c =>
            {
                c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
                {
                    [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public override void Configure(IApplicationBuilder app)
        {
            base.Configure(app);
        }

        protected override void RegisterContainer(IServiceProvider services)
        {
            base.RegisterContainer(services);

            var cfg = new ApiConfig()
            {
            };

            var apiHost = new ApiHost(cfg)
                .WithContainer(Container);
        }
    }
}
