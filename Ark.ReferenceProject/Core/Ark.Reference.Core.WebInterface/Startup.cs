using Ark.Reference.Core.Application;
using Ark.Reference.Core.Common;
using Ark.Reference.Core.Common.Auth;
using Ark.Reference.Core.WebInterface.Utils;
using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.Core;

using Asp.Versioning;
using Asp.Versioning.Conventions;

using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

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

            var specflowScheme = "SpecFlow";
            var isSpecflow = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "SpecFlow";

            var schemes = new List<string>();

            var authBuilder = services.AddAuthentication();

            services.AddScoped<IClaimsTransformation, TransformEmailClaim>();

            if (isSpecflow)
            {
                schemes.Add(specflowScheme);
                authBuilder.AddJwtBearerArkDefault(specflowScheme, AuthConstants.SpecflowAudience, AuthConstants.SpecflowDomain, o =>
                {
                    o.TokenValidationParameters.ValidIssuer = o.Authority;
                    o.Authority = null;
                    o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.SpecFlowEncryptionKey));
                    o.TokenValidationParameters.RoleClaimType = AuthConstants.ClaimRole;
                });

                services.ArkConfigureSwaggerIdentityServer(AuthConstants.SpecflowDomain, AuthConstants.SpecflowAudience, "notneededundertest");
            }
            else
            {
                schemes.Add("smart");
                services.ConfigureAuthentication(Configuration);

                services.ArkConfigureSwaggerAzureB2C(
                          Configuration["AzureAdB2C:Instance"]
                        , Configuration["AzureAdB2C:Domain"]
                        , Configuration["AzureAdB2C:ClientId"]
                        , Configuration["AzureAdB2C:SignUpSignInPolicyId"]
                        , Configuration["AzureAdB2C:ApiId"]
                );

                services.ConfigureSwaggerGen(c =>
                {
                    var dict = new OpenApiSecurityRequirement
                    {
                        { new OpenApiSecurityScheme { Type = SecuritySchemeType.OAuth2 }, new[] { "openid" } }
                    };
                    c.AddSecurityRequirement(dict);
                    c.CustomSchemaIds((type) => ReflectionHelper.GetCSTypeName(type));
                    c.IncludeXmlCommentsForAssembly<Startup>();
                    c.SchemaFilter<MatrixSchemaFilter>();
                    c.DocumentFilter<AddUserImpersonationScope>();

                    c.OperationFilter<MultiPartJsonOperationFilter>();
                    c.SelectSubTypesUsing(t =>
                    {
                        if (t.IsGenericTypeDefinition) return Enumerable.Empty<Type>();
                        return t.Assembly.GetExportedTypes()
                             .Where(subType => subType.IsSubclassOf(t) && !subType.IsGenericTypeDefinition);
                    });

                    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
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

            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = schemes.First();
                o.DefaultChallengeScheme = schemes.First();
            });

            var claimValues = new List<string> { "mdmadmin:arktest", "admin" };

            var defaultPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(schemes.ToArray())
                .RequireAuthenticatedUser()
                .RequireClaim("arkive_permissions", claimValues.ToArray())
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

            services.AddTransient(s => ControllerNameConvention.Original);
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
