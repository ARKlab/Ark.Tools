using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.AspNetCore.Startup;
using Ark.Tools.AspNetCore.Swashbuckle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
		public Startup(IConfiguration configuration)
			: base(configuration)
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

				c.AddPolymorphismSupport<Polymorphic>();

				//c.OperationFilter<SecurityRequirementsOperationFilter>();

				c.SchemaFilter<ExampleSchemaFilter<Entity.V1.Output>>(Examples.GeEntityPayload()); //Non funziona
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public override void Configure(IApplicationBuilder app)
		{
			base.Configure(app);

		}

		protected override void RegisterContainer(IApplicationBuilder app)
		{
			base.RegisterContainer(app);

			var cfg = new ApiConfig()
			{
			};

			var apiHost = new ApiHost(cfg)
				.WithContainer(Container);

			var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
		}
	}
}
