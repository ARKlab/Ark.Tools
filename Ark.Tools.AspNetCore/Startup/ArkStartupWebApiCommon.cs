// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.Core;
using Ark.Tools.Nodatime;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;
using Swashbuckle.AspNetCore.SwaggerUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Ark.Tools.AspNetCore.Startup
{
	public abstract class ArkStartupWebApiCommon
	{
		public IConfiguration Configuration { get; }

		public Container Container { get; } = new Container();

		public ArkStartupWebApiCommon(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public virtual void ConfigureServices(IServiceCollection services)
		{
			services.AddHttpContextAccessor();
			services.AddLocalization();
			services.AddRouting();

			//ProblemDetails
			services.AddArkProblemDetails();

			// Add minumum framework services.
			services.AddControllers(opt =>
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
				.SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
				.AddNewtonsoftJson(s =>
				{
					s.SerializerSettings.TypeNameHandling = TypeNameHandling.None;
					s.SerializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
					s.SerializerSettings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
					s.SerializerSettings.ConfigureForNodaTimeRanges();
					s.SerializerSettings.Converters.Add(new StringEnumConverter());
					//s.SerializerSettings.ContractResolver = new DefaultContractResolver();
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
			});

			services.AddVersionedApiExplorer(o =>
			{
				o.GroupNameFormat = "'v'VVVV";
				o.SubstituteApiVersionInUrl = true;
				o.SubstitutionFormat = "VVVV";
			});

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
				c.MaxDisplayedTags(100);
				c.ShowExtensions();
				c.EnableValidator();
			});

			services.AddSwaggerGenNewtonsoftSupport();

			//	Api Behaviour override for disabling automatic Problem details
			services.ConfigureOptions<ApiBehaviourOptionsSetup>();

			services.Replace(ServiceDescriptor.Singleton<FormatFilter, CompatibleOldQueryFormatFilter>());
			_integrateSimpleInjectorContainer(services);

			services.AddTransient(s => s.GetRequiredService<IHttpContextAccessor>().HttpContext?.Features?.Get<RequestTelemetry>());
		}

		private void _integrateSimpleInjectorContainer(IServiceCollection services)
		{
			Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
			services.AddSimpleInjector(Container, o =>
			{
				o.AddAspNetCore().AddControllerActivation();
				o.CrossWire<RequestTelemetry>();
				o.CrossWire<TelemetryClient>();
			});

			//services.EnableSimpleInjectorCrossWiring(Container);
			services.AddSingleton<IControllerActivator>(
				new SimpleInjectorControllerActivator(Container));

			services.AddSingleton<IViewComponentActivator>(
				new SimpleInjectorViewComponentActivator(Container));

			services.UseSimpleInjectorAspNetRequestScoping(Container);
		}

		public abstract IEnumerable<ApiVersion> Versions { get; }
		public abstract OpenApiInfo MakeInfo(ApiVersion version);

		public virtual void Configure(IApplicationBuilder app)
		{
			RegisterContainer(app);

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
				endpoints.MapControllers();
			});

			//app.UseMvc(_mvcRoute); //Not Usable without setting 	MVC opt.EnableEndpointRouting = false;
		}

		protected virtual void _mvcRoute(IRouteBuilder routeBuilder)
		{
			routeBuilder.SetTimeZoneInfo(TimeZoneInfo.Utc);
		}

		protected virtual void RegisterContainer(IApplicationBuilder app)
		{
			app.UseSimpleInjector(Container);
			Container.RegisterAuthorizationAspNetCoreUser(app);
		}
	}
}
