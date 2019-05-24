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
			services.AddOData();

			//MVC
			services.AddMvcCore(options =>
			{
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
				c.OperationFilter<ResponseFormatFilter>();
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

			var env = app.ApplicationServices.GetService<IHostingEnvironment>();
		}
	}
}
