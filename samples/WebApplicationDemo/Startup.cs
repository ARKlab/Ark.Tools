using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;

using Asp.Versioning;
using Asp.Versioning.Conventions;

using MessagePack.NodaTime;
using MessagePack.Resolvers;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.OpenApi;

using Swashbuckle.AspNetCore.SwaggerUI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using WebApplicationDemo.Application.Host;
using WebApplicationDemo.Configuration;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo
{
    public class Startup : ArkStartupWebApi
    {
        public Startup(IConfiguration configuration, IHostEnvironment env)
            : base(configuration, env, false)
        {
        }

        public override IEnumerable<ApiVersion> Versions => ApiVersions.All;

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

            var resolver = CompositeResolver.Create([
                NodatimeResolver.Instance,
                DynamicEnumAsStringResolver.Instance,
                StandardResolver.Instance
            ]);

            services.AddMessagePackFormatter(resolver);

            //HealthChecks
            services.AddHealthChecks()
                //.AddCheck<ExampleHealthCheck>("Example Web App Demo Health Check", tags: new string[]{ "ArkTools", "WebDemo"})
                .AddSimpleInjectorCheck<ExampleHealthCheck>(name: "Example SimpleInjector Check", failureStatus: HealthStatus.Unhealthy, tags: ["Example"])
                .AddSimpleInjectorLambdaCheck<IExampleHealthCheckService>(name: "Example SimpleInjector Lamda Check", (adapter, ctk) => adapter.CheckHealthAsync(ctk), failureStatus: HealthStatus.Unhealthy, tags: ["Example"])
                .AddSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Logs;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True", healthQuery: "SELECT 1;", name: "NLOG DB", tags: ["NLOG", "SQLServer"])
                ;

            services.AddArkHealthChecksUIOptions(setup =>
            {
                if (File.Exists(Path.Combine(Environment.CurrentDirectory, "UIHealthChecks.css")))
                    setup.AddCustomStylesheet("UIHealthChecks.css");

                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIHealthChecks.css")))
                    setup.AddCustomStylesheet(AppDomain.CurrentDomain.BaseDirectory + "UIHealthChecks.css");
            });

            bool isAuth0 = string.IsNullOrWhiteSpace(Configuration["AzureAdB2C:Domain"]) ? true : false;

            if (!isAuth0)
            {
                authBuilder.AddMicrosoftIdentityWebApi(options =>
                {
                    Configuration.Bind("AzureAdB2C", options);

                    options.TokenValidationParameters.NameClaimType = "name";

                    (options.TokenHandlers[0] as JsonWebTokenHandler)?.InboundClaimTypeMap.Add("extension_Scope", "scope");
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
                c.MaxDisplayedTags(10000);
                c.DefaultModelRendering(ModelRendering.Model);
                c.ShowExtensions();
                //c.OAuthAppName("Public API");
            });

            services.ConfigureSwaggerGen(c =>
            {
                c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
                {
                    [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
                });

                c.SelectDiscriminatorNameUsing(t => "kind");
                c.SelectDiscriminatorValueUsing(t => t.Name);

            });

            services.Configure<ODataOptions>(options =>
            {
                // make the Odata independent from 'the hosting timezone'. Set this to the Timezone of 'DATA', if any, or UTC, so that is not dependant on the PC/Server Local timezone (reliable results ...)
                options.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
            });

            //https://github.com/dotnet/aspnet-api-versioning/wiki/Controller-Conventions
            services.AddTransient(s => ControllerNameConvention.Original);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public override void Configure(IApplicationBuilder app)
        {
            base.Configure(app);
        }

        protected override void RegisterContainer(IServiceProvider services)
        {
            base.RegisterContainer(services);

            var ext = services.GetService<IExternalInjected>();

            var cfg = new ApiConfig()
            {
            };





            var apiHost = new ApiHost(cfg)
                .WithContainer(Container);
        }
    }
}
