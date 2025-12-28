using Ark.Reference.Core.Application;
using Ark.Reference.Core.Common;
using Ark.Reference.Core.Common.Auth;
using Ark.Reference.Core.WebInterface.JsonContext;
using Ark.Reference.Core.WebInterface.Utils;
using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;

using Asp.Versioning;

using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using NodaTime;

using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;

using Swashbuckle.AspNetCore.SwaggerUI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ark.Reference.Core.WebInterface
{

    public class Startup : ArkStartupWebApi
    {
        public override IEnumerable<ApiVersion> Versions => ApplicationConstants.Versions.Reverse().Select(x => ApiVersionParser.Default.Parse(x));

        public override OpenApiInfo MakeInfo(ApiVersion version)
                => new()
                {
                    Title = "Core Service API",
                    Version = version.ToString("VVVV", CultureInfo.InvariantCulture),
                };

        public Startup(IConfiguration config, IWebHostEnvironment webHostEnvironment)
        : base(config, webHostEnvironment, useNewtonsoftJson: false)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            // Configure System.Text.Json source generation with Ark defaults
            // Using JsonTypeInfoResolver.Combine to merge application and ProblemDetails contexts
            // See: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
            
            // Create contexts using the Default instances (parameterless constructor pattern)
            var coreApiContext = CoreApiJsonSerializerContext.Default;
            var problemDetailsContext = Ark.Tools.AspNetCore.JsonContext.ArkProblemDetailsJsonSerializerContext.Default;
            
            // Combine source-generated contexts with minimal reflection fallback
            // The fallback is required only for Hellang.Middleware.ProblemDetails internal types
            // (DeveloperProblemDetailsExtensions.ErrorDetails) when IncludeExceptionDetails is enabled.
            // This type is internal to the library and cannot be referenced in source generation.
            // In production, exception details are typically disabled, so reflection is rarely used.
            // The source-generated contexts handle 99%+ of serialization for optimal performance.
            var combinedResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                coreApiContext,                              // Application types - source generated (Priority 1)
                problemDetailsContext,                        // Error types - source generated (Priority 2)
                new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()); // Fallback for middleware internals (Priority 3)

            services.ConfigureHttpJsonOptions(options =>
            {
                // Configure Ark defaults and set the combined resolver
                // This ensures all JsonSerializerOptions use consistent Ark configuration
                System.Text.Json.Extensions.ConfigureArkDefaults(options.SerializerOptions);
                options.SerializerOptions.TypeInfoResolver = combinedResolver;
            });

            services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            {
                // Configure Ark defaults and set the combined resolver
                // Reusing the same configuration pattern as HttpJsonOptions
                System.Text.Json.Extensions.ConfigureArkDefaults(options.JsonSerializerOptions);
                options.JsonSerializerOptions.TypeInfoResolver = combinedResolver;
            });

            var integrationTestsScheme = "IntegrationTests";
            var isIntegrationTests = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "IntegrationTests";

            var schemes = new List<string>();

            var authBuilder = services.AddAuthentication();

            services.AddScoped<IClaimsTransformation, TransformEmailClaim>();

            if (isIntegrationTests)
            {
                schemes.Add(integrationTestsScheme);
                authBuilder.AddJwtBearerArkDefault(integrationTestsScheme, AuthConstants.IntegrationTestsAudience, AuthConstants.IntegrationTestsDomain, o =>
                {
                    o.TokenValidationParameters.ValidIssuer = o.Authority;
                    o.Authority = null;
                    o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.IntegrationTestsEncryptionKey));
                    o.TokenValidationParameters.RoleClaimType = AuthConstants.ClaimRole;
                });

                services.ArkConfigureSwaggerIdentityServer(AuthConstants.IntegrationTestsDomain, AuthConstants.IntegrationTestsAudience, "notneededundertest");
            }
            else
            {
                schemes.Add(JwtBearerDefaults.AuthenticationScheme);
                authBuilder.AddMicrosoftIdentityWebApi(Configuration.GetRequiredSection(AuthConstants.EntraIdSchema));

                services.ArkConfigureSwaggerEntraId(Configuration.GetRequiredValue<string>("EntraId:Instance")
                        , Configuration.GetRequiredValue<string>("EntraId:Domain")
                        , Configuration.GetRequiredValue<string>("EntraId:ClientId")
                        , Configuration.GetRequiredValue<string>("EntraId:TenantId"));

                services.ConfigureSwaggerGen(c =>
                {
                    c.IncludeXmlCommentsForAssembly<Startup>();
                    c.SchemaFilter<MatrixSchemaFilter>();

                    c.OperationFilter<MultiPartJsonOperationFilter>();
                });

                services.ArkConfigureSwaggerUI(c =>
                {
                    c.MaxDisplayedTags(100);
                    c.DefaultModelRendering(ModelRendering.Example);
                    c.DefaultModelsExpandDepth(2);
                    c.ShowExtensions();
                    c.OAuthAppName("Core API");

                    c.ConfigObject.TryItOutEnabled = false;
                });
            }

            var defaultPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(schemes.ToArray())
                .RequireAuthenticatedUser()
                .Build();


            services.AddMvcCore()
                .AddMvcOptions(opt =>
                {
                    opt.Filters.Add(new AuthorizeFilter(defaultPolicy));

                    opt.Conventions.Add(new ApiControllerConvention());

                    // add custom model binders to beginning of collection
                    opt.ModelBinderProviders.Insert(0, new FormDataJsonBinderProvider(opt.InputFormatters));
                });

            services.Configure<SnapshotCollectorConfiguration>(o =>
            {
                o.IsLowPrioritySnapshotUploader = false;
            });
        }

        protected override void RegisterContainer(IServiceProvider services)
        {
            base.RegisterContainer(services);

            var api = Configuration.BuildApiHost()
                .WithContainer(Container)
                .WithIClock(services.GetService<IClock>())
                .WithAuthorization()
                .WithRebus(Application.Host.Queue.OneWay, services.GetService<InMemNetwork>(),
                    services.GetService<InMemorySubscriberStore>())
                ;
        }
    }
}
