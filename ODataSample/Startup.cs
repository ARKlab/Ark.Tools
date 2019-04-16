using System.Collections.Generic;
using Ark.Tools.AspNetCore.Startup;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using ODataSample.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.Net.Http.Headers;
using System.Linq;
using Microsoft.EntityFrameworkCore.Design;
using Ark.Tools.EntityFrameworkCore.Nodatime;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ark.Tools.AspNetCore;
using Ark.Tools.Solid;
using System.Security.Claims;
using Ark.Tools.EntityFrameworkCore.SystemVersioning.Audit;
using Ark.Tools.EntityFrameworkCore.SystemVersioning;
using Microsoft.AspNet.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.AspNet.OData;
using ODataSample.Controllers;
using static Microsoft.AspNet.OData.Query.AllowedQueryOptions;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace ODataSample
{
    public class Startup : ArkStartupWebApi
    {

        public Startup(IConfiguration configuration) 
            : base(configuration)
        {
        }

        public override IEnumerable<ApiVersion> Versions => new[] { new ApiVersion(1,0) };

        public override Info MakeInfo(ApiVersion version)
            => new Info
            {
                Title = "API",
                Version = version.ToString("VVVV"),
            };

        public override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

			//MVC
			services.AddMvc(options =>
			{
				// https://blogs.msdn.microsoft.com/webdev/2018/08/27/asp-net-core-2-2-0-preview1-endpoint-routing/
				// Because conflicts with ODataRouting as of this version
				// could improve performance though
				options.EnableEndpointRouting = false;
			});

			services.AddMvcCore(options =>
			{
				foreach (var outputFormatter in options.OutputFormatters.OfType<ODataOutputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
				{
					outputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));
				}
				foreach (var inputFormatter in options.InputFormatters.OfType<ODataInputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
				{
					inputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));
				}
			});

			//OData
			services.AddOData().EnableApiVersioning();
			//services.AddODataQueryFilter(new EnableQueryAttribute
			//{
			//	AllowedQueryOptions = AllowedQueryOptions.All,
			//	PageSize = 10,
			//	MaxNodeCount = 20,
			//});

			services.AddODataApiExplorer(
                options =>
                {
                    // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                    // note: the specified format code will format the version as "'v'major[.minor][-status]"
                    options.GroupNameFormat = "'v'VVVV";

                    // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                    // can also be used to control the format of the API version in route templates
                    options.SubstituteApiVersionInUrl = true;

					options.QueryOptions.Controller<BooksController>()
					.Action(c => c.Get(default)).Allow(Skip | Count).AllowTop(100);
				});


			services.AddTransient<IContextProvider<ClaimsPrincipal>, AspNetCoreUserContextProvider>();

			//Entity Framework DB Context
			services.AddDbContext<BookStoreContext>((provider, options) =>
            {
                options.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=ODataSample;Integrated Security=True;MultipleActiveResultSets=true");

				options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

				//For Nodatime Support
				options.AddNodaTimeSqlServer();

				//For System Versioning And Audit
				options.AddSqlServerSystemVersioningAudit();
			});
		}

		public class NodatimeDesignTime : IDesignTimeServices
		{
			public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
			{				
				serviceCollection.SetNodaTimeSqlServerMappingSource();
				//Debugger.Launch();
			}
		}

		private IApplicationBuilder _app;

		public override void Configure(IApplicationBuilder app)
		{
            _app = app; // :(

			base.Configure(app);
        }

        protected override void _mvcRoute(IRouteBuilder routeBuilder)
        {
            var mb = _app.ApplicationServices.GetService<VersionedODataModelBuilder>();
			routeBuilder.EnableDependencyInjection();

			routeBuilder.MapVersionedODataRoutes("odata", "v{api-version:apiVersion}/odata", mb.GetEdmModels());
        }
	}
}
