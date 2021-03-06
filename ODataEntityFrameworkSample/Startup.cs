﻿using System.Collections.Generic;
using Ark.Tools.AspNetCore.Startup;
using Microsoft.AspNet.OData.Builder;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using ODataEntityFrameworkSample.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.Net.Http.Headers;
using System.Linq;
using Ark.Tools.EntityFrameworkCore.Nodatime;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ark.Tools.EntityFrameworkCore.SystemVersioning;
using ODataEntityFrameworkSample.Controllers;
using static Microsoft.AspNet.OData.Query.AllowedQueryOptions;
using Ark.Tools.Solid;
using System.Security.Claims;
using Ark.Tools.AspNetCore;
using AutoMapper;
using AutoMapper.EquivalencyExpression;
using AutoMapper.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

namespace ODataEntityFrameworkSample
{
	public class Startup : ArkStartupWebApi
    {

        public Startup(IConfiguration configuration) 
            : base(configuration)
        {
        }

        public override IEnumerable<ApiVersion> Versions => new[] { new ApiVersion(1,0) };

        public override OpenApiInfo MakeInfo(ApiVersion version)
            => new OpenApiInfo
			{
                Title = "API",
                Version = version.ToString("VVVV"),
            };

        public override void ConfigureServices(IServiceCollection services)
        {
			//Debugger.Launch();

			base.ConfigureServices(services);

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

					options.QueryOptions.Controller<CountriesController>()
					.Action(c => c.Get(default)).Allow(Skip | Count).AllowTop(100);
				});

			//MVC
			services.AddMvcCore(options =>
			{
				// https://blogs.msdn.microsoft.com/webdev/2018/08/27/asp-net-core-2-2-0-preview1-endpoint-routing/
				// Because conflicts with ODataRouting as of this version
				// could improve performance though
				options.EnableEndpointRouting = false;

				foreach (var outputFormatter in options.OutputFormatters.OfType<ODataOutputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
				{
					outputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));
				}
				foreach (var inputFormatter in options.InputFormatters.OfType<ODataInputFormatter>().Where(_ => _.SupportedMediaTypes.Count == 0))
				{
					inputFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/prs.odatatestxx-odata"));
				}
			});

			//Claims Principal --> REMOVE!!
			services.AddTransient<IContextProvider<ClaimsPrincipal>, AspNetCoreUserContextProvider>();

			//Entity Framework DB Context
			services.AddDbContext<ODataSampleContext>((provider, options) =>
            {
                options.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=ODataSample;Integrated Security=True;MultipleActiveResultSets=true");

				options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

				//For Nodatime Support
				options.AddNodaTimeSqlServer();

				//For System Versioning And Audit
				options.AddSqlServerSystemVersioningAudit();
			});

			//services.SetNodaTimeSqlServerMappingSource();

			Mapper.Initialize(cfg =>
			{
				cfg.AddCollectionMappers();
				cfg.UseEntityFrameworkCoreModel<ODataSampleContext>(services);
				// Configuration code

				//cfg.CreateMap<Book, BookDto>();
				//cfg.CreateMap<ICollection<Address>, IEnumerable<AddressDto>>();
				//cfg.CreateMap<Press, PressDto>();
				//cfg.CreateMap<Bibliografy, BibliografyDto>();
				//cfg.CreateMap<ICollection<Code>, IEnumerable<CodeDto>>();


				cfg.CreateMap<Country, CountryDto>();
				cfg.CreateMap<ICollection<City>, IEnumerable<CityDto>>();

				cfg.CreateMap<School, SchoolDto>();
				cfg.CreateMap<ICollection<Student>, IEnumerable<StudentDto>>();
				cfg.CreateMap<Registry, RegistryDto>();
				cfg.CreateMap<ICollection<Rule>, IEnumerable<RuleDto>>();

				cfg.CreateMap<University, University>();
				cfg.CreateMap<Person, Person>();

				cfg.CreateMap<PhotoStudio, PhotoStudio>();
				cfg.CreateMap<Worker, Worker>();

			});
		}

		private IApplicationBuilder _app;

		public override void Configure(IApplicationBuilder app)
		{
            _app = app; // :(

			base.Configure(app);
        }

        protected override void _mvcRoute(IRouteBuilder routeBuilder)
        {
			base._mvcRoute(routeBuilder);

			var mb = _app.ApplicationServices.GetService<VersionedODataModelBuilder>();
			routeBuilder.EnableDependencyInjection();

			routeBuilder.MapVersionedODataRoutes("odata", "v{api-version:apiVersion}/odata", mb.GetEdmModels());
        }
	}
}
