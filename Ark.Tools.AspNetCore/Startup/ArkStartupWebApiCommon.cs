// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.Core;
using Ark.Tools.Nodatime;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using SimpleInjector;
using SimpleInjector.Integration.AspNetCore.Mvc;
using SimpleInjector.Lifestyles;
using Swashbuckle.AspNetCore.Swagger;
using System.Collections.Generic;
using System.Globalization;

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
            services.AddLocalization();

            // Add minumum framework services.
            services.AddMvcCore()
                .AddVersionedApiExplorer(o => o.GroupNameFormat = "'v'VVVV")
                .AddAuthorization()
                .AddFormatterMappings()
                .AddDataAnnotations()
                .AddJsonFormatters()
            ;

            services.AddApiVersioning(o =>
            {
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = false;
            });

            services.AddMvcCore()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddMvcOptions(opt =>
                {
                    opt.UseCentralRoutePrefix(new RouteAttribute("v{api-version:apiVersion}"));

                    opt.Filters.Add(new ArkDefaultExceptionFilter());
                    opt.Filters.Add(new ProducesAttribute("application/json"));
                    opt.Filters.Add(new ConsumesAttribute("application/json"));
                    opt.Filters.Add(new ResponseCacheAttribute()
                    {
                        Location = ResponseCacheLocation.Any,
                        Duration = 0,
                        NoStore = false,
                        VaryByHeader = "Accept,Accept-Encoding,Accept-Language,Authorization"
                    });
                    opt.Filters.Add(new ModelStateValidationFilter());
                    opt.Filters.Add(new ETagHeaderBasicSupportFilter());

                    opt.ReturnHttpNotAcceptable = true;
                    opt.RespectBrowserAcceptHeader = true;
                })
                .AddJsonOptions(s =>
                {
                    s.SerializerSettings.TypeNameHandling = TypeNameHandling.None;
                    s.SerializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    s.SerializerSettings.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
                    s.SerializerSettings.ConfigureForNodaTimeRanges();
                    s.SerializerSettings.Converters.Add(new StringEnumConverter());
                    //s.SerializerSettings.ContractResolver = new DefaultContractResolver();
                });
            ;


            services.AddSwaggerGen(c =>
            {
                c.DocInclusionPredicate((docName, apiDesc) => apiDesc.GroupName == docName);

                c.MapNodaTimeTypes();

                c.OperationFilter<RemoveVersionParameters>();
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
                c.MaxDisplayedTags(5);
                c.ShowExtensions();
                c.EnableValidator();
            });

            services.Replace(ServiceDescriptor.Singleton<FormatFilter, CompatibleOldQueryFormatFilter>());
            _integrateSimpleInjectorContainer(services);
            
            services.AddTransient(s => s.GetRequiredService<IHttpContextAccessor>().HttpContext?.Features?.Get<RequestTelemetry>());
        }

        private void _integrateSimpleInjectorContainer(IServiceCollection services)
        {
            Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            services.EnableSimpleInjectorCrossWiring(Container);
            services.AddSingleton<IControllerActivator>(
                new SimpleInjectorControllerActivator(Container));

            services.AddSingleton<IViewComponentActivator>(
                new SimpleInjectorViewComponentActivator(Container));

            services.UseSimpleInjectorAspNetRequestScoping(Container);
        }

        public abstract IEnumerable<ApiVersion> Versions { get; }
        public abstract Info MakeInfo(ApiVersion version);

        public virtual void Configure(IApplicationBuilder app)
        {
            RegisterContainer(app);

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
                SupportedCultures = CultureInfo.GetCultures(CultureTypes.InstalledWin32Cultures | CultureTypes.NeutralCultures | CultureTypes.SpecificCultures)
            });

            app.ArkUseSwagger();
            app.ArkUseSwaggerUI();
            app.UseAuthentication();
            app.UseMvc();
        }

        protected virtual void RegisterContainer(IApplicationBuilder app)
        {
            Container.AutoCrossWireAspNetComponents(app);
            Container.RegisterMvcControllers(app);
            Container.RegisterAuthorizationAspNetCoreUser(app);
        }
    }
}
