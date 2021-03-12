using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using WebApplicationDemo.Application.Host;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo
{
	public class Startup : ArkStartupWebApi
	{
		public Startup(IConfiguration configuration, IHostEnvironment env)
			: base(configuration, env, false)
		{
        }

		public override IEnumerable<ApiVersion> Versions => new[] { new ApiVersion(1, 0) };

        public override OpenApiInfo MakeInfo(ApiVersion version)
			=> new OpenApiInfo
			{
				Title = "API",
				Version = version.ToString("VVVV"),
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

			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = auth0Scheme;
				options.DefaultChallengeScheme = auth0Scheme;

			})
			.AddJwtBearerArkDefault(auth0Scheme, audience, domain, o =>
			{
				if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "SpecFlow")
				{
					o.TokenValidationParameters.ValidIssuer = o.Authority;
					o.Authority = null;
					//o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(AuthConstants.ClientSecretSpecFlow));
				}
				o.TokenValidationParameters.RoleClaimType = "Role";
			})
			;

			//HealthChecks
			services.AddHealthChecks()
				//.AddCheck<ExampleHealthCheck>("Example Web App Demo Health Check", tags: new string[]{ "ArkTools", "WebDemo"})
				.AddSimpleInjectorCheck<ExampleHealthCheck>(name: "Example SimpleInjector Check", failureStatus: HealthStatus.Unhealthy, tags: new string[] { "Example" })
				.AddSimpleInjectorLambdaCheck<IExampleHealthCheckService>(name: "Example SimpleInjector Lamda Check", (adapter, ctk) => adapter.CheckHealthAsync(ctk), failureStatus: HealthStatus.Unhealthy, tags: new string[] { "Example" })
				.AddSqlServer("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Logs;Integrated Security=True;Persist Security Info=False;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=60;Encrypt=False;TrustServerCertificate=True", healthQuery: "SELECT 1;", name: "NLOG DB", tags: new string[] { "NLOG", "SQLServer" })
				;

			services.AddArkHealthChecksUIOptions(setup =>
			{
                if (File.Exists(Path.Combine(Environment.CurrentDirectory, "UIHealthChecks.css")))
                    setup.AddCustomStylesheet("UIHealthChecks.css");

                if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIHealthChecks.css")))
					setup.AddCustomStylesheet((String)AppDomain.CurrentDomain.BaseDirectory + "UIHealthChecks.css");
			});

			services.ArkConfigureSwaggerAuth0(domain, audience, swaggerClientId);

			services.ArkConfigureSwaggerUI(c =>
			{
				c.MaxDisplayedTags(100);
				c.DefaultModelRendering(ModelRendering.Model);
				c.ShowExtensions();
				//c.OAuthAppName("Public API");
			});

			services.ConfigureSwaggerGen(c =>
			{
				var dict = new OpenApiSecurityRequirement
				{
					{ new OpenApiSecurityScheme { Type = SecuritySchemeType.OAuth2 }, new[] { "openid" } }
				};

				c.AddSecurityRequirement(dict);

				c.AddPolymorphismSupport<Polymorphic>("kind");

				//c.OperationFilter<SecurityRequirementsOperationFilter>();

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

			var ext = services.GetService<IExternalInjected>();

			var cfg = new ApiConfig()
			{
			};

			var apiHost = new ApiHost(cfg)
				.WithContainer(Container);
		}
	}
}
