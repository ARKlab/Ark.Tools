// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.Core;

using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

using Newtonsoft.Json;

using SimpleInjector;

using Swashbuckle.AspNetCore.SwaggerUI;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ark.Tools.AspNetCore.Startup
{
    public abstract class ArkStartupWebApiCommon
	{
		public IConfiguration Configuration { get; }
		public bool UseNewtonsoftJson { get; }
		public Container Container { get; } = new Container();
		public IHostEnvironment HostEnvironment { get; }

		public ArkStartupWebApiCommon(IConfiguration configuration, IHostEnvironment hostEnvironment)
			: this(configuration, hostEnvironment, false)
		{
		}

		public ArkStartupWebApiCommon(IConfiguration configuration, IHostEnvironment hostEnvironment, bool useNewtonsoftJson)
		{
			Configuration = configuration;
			UseNewtonsoftJson = useNewtonsoftJson;
            HostEnvironment = hostEnvironment;
        }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddLocalization();
            services.AddRouting();

            //ProblemDetails
            services.AddArkProblemDetails();

            //HealthChecks
            services.AddArkHealthChecks();

            // Add minumum framework services.
            var mvcBuilder = services
                .AddControllers(opt =>
                {
                    //Conventions
                    opt.Conventions.Add(new ProblemDetailsResultApiConvention());
                    opt.UseCentralRoutePrefix(new RouteAttribute("v{api-version:apiVersion}"));

                    opt.Filters.Add(new ArkDefaultExceptionFilter());
                    //opt.Filters.Add(new ProducesAttribute("application/json")); // To be specified after
                    //opt.Filters.Add(new ConsumesAttribute("application/json")); // broken in aspnetcore 2.2 as is enforced on GET too
                    opt.Conventions.Add(new ArkDefaultConventions());
                    opt.Filters.Add(new ResponseCacheAttribute()
                    {
                        Location = ResponseCacheLocation.Any,
                        Duration = 0,
                        NoStore = false,
                        VaryByHeader = "Accept,Accept-Encoding,Accept-Language,Authorization"
                    });
                    opt.Filters.Add(new ModelStateValidationFilter());
                    opt.Filters.Add(new ETagHeaderBasicSupportFilter());
                    opt.Filters.Add(new ApiControllerAttribute());
                    opt.ReturnHttpNotAcceptable = true;
                    opt.RespectBrowserAcceptHeader = true;
                })
                .AddOData(options =>
                {
                    options.Count().Select().OrderBy();
                    options.RouteOptions.EnableKeyInParenthesis = false;
                    options.RouteOptions.EnableNonParenthesisForEmptyParameterFunction = true;
                    options.RouteOptions.EnablePropertyNameCaseInsensitive = true;
                    options.RouteOptions.EnableQualifiedOperationCall = false;
                    options.RouteOptions.EnableUnqualifiedOperationCall = true;
                })
                .AddFormatterMappings(s =>
                {
                })
                ;


            services.AddAuthorization();

            services.AddApiVersioning(o =>
            {
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = Versions.Last();
            })
            .AddOData(options => options.AddRouteComponents("api"))
            .AddODataApiExplorer(options =>
            {
                // add the versioned api explorer, which also adds IApiVersionDescriptionProvider service
                // note: the specified format code will format the version as "'v'major[.minor][-status]"
                options.GroupNameFormat = "'v'VVVV";

                // note: this option is only necessary when versioning by url segment. the SubstitutionFormat
                // can also be used to control the format of the API version in route templates
                options.SubstituteApiVersionInUrl = true;
            })
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'VVVV";
                o.SubstituteApiVersionInUrl = true;
                o.SubstitutionFormat = "VVVV";
            })
            ;

            services.AddTransient<IActionDescriptorProvider, RemoveODataQueryOptionsActionDescriptorProvider>();

            services.AddSwaggerGen(c =>
            {
                c.DocInclusionPredicate((docName, apiDesc) => apiDesc.GroupName == docName);

                c.MapNodaTimeTypes();

                c.OperationFilter<ODataParamsOnSwagger>();
                c.OperationFilter<SupportFlaggedEnums>();

                c.OperationFilter<PrettifyOperationIdOperationFilter>();
                c.SchemaFilter<RequiredSchemaFilter>();

                c.DocumentFilter<SetVersionInPaths>();

                c.OperationFilter<DefaultResponsesOperationFilter>();

                c.IncludeXmlCommentsForAssembly(this.GetType().Assembly);

                c.CustomSchemaIds((type) => ReflectionHelper.GetCSTypeName(type).Replace($"{type.Namespace}.", @""));
                c.EnableAnnotations();
            });

            services.ArkConfigureSwaggerVersions(Versions, MakeInfo);

            services.ArkConfigureSwagger(c =>
            {
                c.RouteTemplate = "swagger/docs/{documentName}";
            });

            services.ArkConfigureSwaggerUI(c =>
            {
                c.RoutePrefix = "swagger";

                c.DefaultModelExpandDepth(2);
                c.DefaultModelRendering(ModelRendering.Model);
                c.DisplayRequestDuration();
                c.DocExpansion(DocExpansion.None);
                c.EnableDeepLinking();
                c.EnableFilter();
                c.EnablePersistAuthorization();
                c.ShowCommonExtensions();
                c.MaxDisplayedTags(100);
                c.ShowExtensions();
                c.EnableValidator();
            });

            if (UseNewtonsoftJson)
				services.AddSwaggerGenNewtonsoftSupport();

			if (UseNewtonsoftJson)
			{
				mvcBuilder.AddNewtonsoftJson(s =>
				{
					s.SerializerSettings.ConfigureArkDefaults();
				});
			}
			else
			{
				mvcBuilder.AddJsonOptions(options =>
				{
					options.JsonSerializerOptions.ConfigureArkDefaults();
				});
			}

			//	Api Behaviour override for disabling automatic Problem details
			services.ConfigureOptions<ApiBehaviourOptionsSetup>();

			services.Replace(ServiceDescriptor.Singleton<FormatFilter, CompatibleOldQueryFormatFilter>());
			_integrateSimpleInjectorContainer(services);

			services.AddTransient(s => s.GetRequiredService<IHttpContextAccessor>().HttpContext?.Features?.Get<RequestTelemetry>()
                ?? throw new InvalidOperationException("Failed to obtain the RequestTelemetry from the current HttpContext. " +
                    "Make sure trying to access RequestTelemetry within a Request context, and not a BackgroundService."));
		}

		private void _integrateSimpleInjectorContainer(IServiceCollection services)
		{
			services.AddSimpleInjector(Container, o =>
			{
				o.AddAspNetCore().AddControllerActivation();

                Container.Options.ContainerLocking += (s,e) =>
				{
					RegisterContainer(o.ApplicationServices);
				};
			});
			RegisterContainer();
		}

        public abstract IEnumerable<ApiVersion> Versions { get; }

        public abstract OpenApiInfo MakeInfo(ApiVersion version);

        public virtual void Configure(IApplicationBuilder app)
        {
            app.UseSimpleInjector(Container);
            app.UseRouting();
            app.UseCors(p => p
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetIsOriginAllowed(_ => true));

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
                SupportedCultures = CultureInfo.GetCultures(CultureTypes.InstalledWin32Cultures | CultureTypes.NeutralCultures | CultureTypes.SpecificCultures)
            });

            app.UseArkProblemDetails();

            app.UseSwagger();
            app.UseSwaggerUI();

            //app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => {

                endpoints.MapArkHealthChecks();
                endpoints.MapControllers();
            });

            //app.UseMvc(_mvcRoute); //Not Usable without setting 	MVC opt.EnableEndpointRouting = false;
        }

        protected virtual void _mvcRoute(IRouteBuilder routeBuilder)
		{
		}

		protected virtual void RegisterContainer()
		{
			Container.RegisterAuthorizationAspNetCoreUser();
		}

		protected virtual void RegisterContainer(IServiceProvider services)
		{
		}
	}
}
