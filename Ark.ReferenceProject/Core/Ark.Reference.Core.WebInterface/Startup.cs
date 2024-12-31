using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Ark.Tools.AspNetCore.Startup;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Configuration;
using Ark.Reference.Core.Common;
using Ark.Tools.AspNetCore.Swashbuckle;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.ApplicationInsights.SnapshotCollector;
using Microsoft.AspNetCore.Authorization;
using Ark.Reference.Core.Common.Auth;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Ark.Reference.Core.WebInterface.Utils;
using Ark.Reference.Core.Application;
using Rebus.Transport.InMem;
using Rebus.Persistence.InMem;
using NodaTime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Swashbuckle.AspNetCore.SwaggerUI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using System.Globalization;

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
                    c.DocumentFilter<AddUserImpersonationScope>();

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
