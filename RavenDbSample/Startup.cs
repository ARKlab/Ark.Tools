using System.Collections.Generic;
using Ark.Tools.AspNetCore.Startup;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.Net.Http.Headers;
using System.Linq;
using Raven.Client.Documents;
using Microsoft.AspNetCore.Hosting;
using RavenDbSample.Application.Host;
using Ark.Tools.RavenDb.Auditing;
using Raven.Client.Documents.Operations.Revisions;
using Ark.Tools.AspNetCore.Swashbuckle;
using RavenDbSample.Utils;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using System;
using Swashbuckle.AspNetCore.SwaggerUI;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace RavenDbSample
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

		public override void ConfigureServices(IServiceCollection services)
		{
			base.ConfigureServices(services);

			var auth0Scheme = "Auth0";
			var audience = Configuration["Auth0:Audience"];
			var domain = Configuration["Auth0:Domain"];
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
					o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(ApplicationConstants.ClientSecretSpecFlow));
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

				//c.AddPolymorphismSupport<Polymorphic>();
				c.SchemaFilter<SwaggerExcludeFilter>();

				//c.OperationFilter<SecurityRequirementsOperationFilter>();

				//c.SchemaFilter<ExampleSchemaFilter<Entity.V1.Output>>(Examples.GeEntityPayload()); //Non funziona
			});

			////OData
			//services.AddOData();

			////MVC
			//services.AddMvcCore(options =>
			//{
			//	foreach (var outputFormatter in options.OutputFormatters.OfType<ODataOutputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
			//		outputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));

			//	foreach (var inputFormatter in options.InputFormatters.OfType<ODataInputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
			//		inputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));
			//});

			//Add HostedService for Auditable
			//services.AddHostedServiceAuditProcessor();

			var assemblies = new List<Assembly>
					{
						Assembly.Load("RavenDbSample"),
					};
			services.AddHostedServiceAuditProcessor(assemblies);


			var store = new DocumentStore()
			{
				Database = "RavenDb",
				Urls = new[]
				{
					"http://127.0.0.1:8080"
				}
			};

			services.AddSingleton(store.Initialize());

			//services.AddTransient<IActionDescriptorProvider, RemoveODataQueryOptionsActionDescriptorProvider>();

			//services.AddSwaggerGen(c =>
			//{
			//	//c.OperationFilter<ODataParamsOnSwagger>();
			//	//c.OperationFilter<ResponseFormatFilter>();
			//	c.SchemaFilter<SwaggerExcludeFilter>();
			//});
		}

		private IApplicationBuilder _app;

		public override void Configure(IApplicationBuilder app)
		{
			_app = app; // :(

			var store = app.ApplicationServices.GetService<IDocumentStore>();

			store.Maintenance.Send(new ConfigureRevisionsOperation(new RevisionsConfiguration
			{
				Default = new RevisionsCollectionConfiguration
				{
					Disabled = false,
					PurgeOnDelete = false,
					MinimumRevisionsToKeep = null,
					MinimumRevisionAgeToKeep = null,
				}
			}));


			base.Configure(app);
		}

		protected override void _mvcRoute(IRouteBuilder routeBuilder)
		{
			base._mvcRoute(routeBuilder);

			routeBuilder.Filter().OrderBy().MaxTop(1000);
			routeBuilder.EnableDependencyInjection();
		}

		protected override void RegisterContainer(IApplicationBuilder app)
		{
			base.RegisterContainer(app);

			var cfg = new ApiConfig()
			{
			};

			var apiHost = new ApiHost(cfg)
				.WithContainer(Container)
				.WithRavenDbAudit();

			var env = app.ApplicationServices.GetService<IWebHostEnvironment>();
		}
	}
}
