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
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Query;
using Ark.Tools.AspNetCore.Swashbuckle;

namespace RavenDbSample
{
	public class Startup : ArkStartupWebApi
	{

		public Startup(IConfiguration configuration)
			: base(configuration)
		{
		}

		public override IEnumerable<ApiVersion> Versions => new[] { new ApiVersion(1, 0) };

		public override Info MakeInfo(ApiVersion version)
			=> new Info
			{
				Title = "API",
				Version = version.ToString("VVVV"),
			};

		public override void ConfigureServices(IServiceCollection services)
		{
			base.ConfigureServices(services);

			//OData
			services.AddOData()/*.EnableApiVersioning()*/;
			//services.AddODataQueryFilter(new EnableQueryAttribute
			//{
			//	AllowedQueryOptions = AllowedQueryOptions.All,
			//	AllowedFunctions = AllowedFunctions.Any,
			//	PageSize = 10,
			//	MaxNodeCount = 20,
			//});

			//services.AddODataApiExplorer(
			//options =>
			//{
			//	// add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
			//	// note: the specified format code will format the version as "'v'major[.minor][-status]"
			//	options.GroupNameFormat = "'v'VVVV";

			//	// note: this option is only necessary when versioning by url segment. the SubstitutionFormat
			//	// can also be used to control the format of the API version in route templates
			//	options.SubstituteApiVersionInUrl = true;
			//});

			//MVC
			services.AddMvcCore(options =>
			{
				//options.EnableEndpointRouting = false; //For Odata

				foreach (var outputFormatter in options.OutputFormatters.OfType<ODataOutputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
					outputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));

				foreach (var inputFormatter in options.InputFormatters.OfType<ODataInputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
					inputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));
			});

			//Add HostedService for Auditable
			services.AddHostedServiceAuditProcessor();

			var store = new DocumentStore()
			{
				Database = "RavenDb",
				Urls = new []
				{
					"http://127.0.0.1:8080"
				}
			};

			services.AddSingleton(store.Initialize());


			services.AddSwaggerGen(c =>
			{
				c.OperationFilter<ODataParamsOnSwagger>();
			});
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

			//var mb = _app.ApplicationServices.GetService<VersionedODataModelBuilder>();
			
			routeBuilder.Filter().OrderBy().MaxTop(1000);
			routeBuilder.EnableDependencyInjection();

			//routeBuilder.MapVersionedODataRoutes("odata", "v{api-version:apiVersion}/odata", mb.GetEdmModels());
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

			var env = app.ApplicationServices.GetService<IHostingEnvironment>();
		}
	}
}
