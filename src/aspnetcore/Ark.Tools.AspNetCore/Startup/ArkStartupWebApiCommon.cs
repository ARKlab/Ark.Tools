// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.HealthChecks;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.AspNetCore.Swashbuckle;
using Ark.Tools.Core;

using Asp.Versioning;

using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.NewtonsoftJson;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;

using Newtonsoft.Json;

using SimpleInjector;

using Swashbuckle.AspNetCore.SwaggerUI;

using System.Globalization;
using System.Text.Json;

namespace Ark.Tools.AspNetCore.Startup;

public abstract class ArkStartupWebApiCommon
{
    public IConfiguration Configuration { get; }
    public bool UseNewtonsoftJson { get; }
    public Container Container { get; } = new Container();
    public IHostEnvironment HostEnvironment { get; }

    protected ArkStartupWebApiCommon(IConfiguration configuration, IHostEnvironment hostEnvironment)
        : this(configuration, hostEnvironment, false)
    {
    }

    protected ArkStartupWebApiCommon(IConfiguration configuration, IHostEnvironment hostEnvironment, bool useNewtonsoftJson)
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

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        // Add minumum framework services.
        var mvcBuilder = services
            .AddControllers(opt =>
            {
                //Conventions
                opt.Conventions.Add(new ProblemDetailsResultApiConvention());
                opt.UseCentralRoutePrefix(new RouteAttribute("v{api-version:apiVersion}"));

                if (!HostEnvironment.IsProduction())
                    opt.Filters.Add(new ArkDefaultExceptionFilterAttribute());

                opt.Conventions.Add(new ArkDefaultConventions());

                opt.Filters.Add(new ResponseCacheAttribute()
                {
                    Location = ResponseCacheLocation.Any,
                    Duration = 0,
                    NoStore = false,
                    VaryByHeader = "Accept,Accept-Encoding,Accept-Language,Authorization"
                });
                opt.Filters.Add(new ModelStateValidationFilterAttribute());
                opt.Filters.Add(new ETagHeaderBasicSupportFilterAttribute());
                opt.Filters.Add(new ApiControllerAttribute());
                opt.ReturnHttpNotAcceptable = true;
                opt.RespectBrowserAcceptHeader = true;
            })
            .AddOData(options =>
            {
                options.EnableQueryFeatures();

                options.RouteOptions.EnableKeyInParenthesis = true;
                options.RouteOptions.EnableNonParenthesisForEmptyParameterFunction = true;
                options.RouteOptions.EnableQualifiedOperationCall = false;
                options.RouteOptions.EnableUnqualifiedOperationCall = true;
                options.RouteOptions.EnableActionNameCaseInsensitive = false;
                options.RouteOptions.EnablePropertyNameCaseInsensitive = false;

                options.EnableNoDollarQueryOptions = false;
                options.EnableAttributeRouting = false;
                options.UrlKeyDelimiter = Microsoft.OData.ODataUrlKeyDelimiter.Parentheses;
            })
            ;

        services.AddODataQueryFilter();

        services.AddAuthorization();

        services.AddApiVersioning(o =>
        {
            o.ReportApiVersions = true;
            o.RouteConstraintName = "apiVersion";
            o.DefaultApiVersion = Versions.Last();
            o.AssumeDefaultVersionWhenUnspecified = true;
        })
        .AddMvc(o =>
        {
        })
        .AddOData(options =>
        {
            options.AddRouteComponents("v{api-version:apiVersion}");

        })
        .AddODataApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVVV";
            options.SubstituteApiVersionInUrl = true;
            options.SubstitutionFormat = "VVVV";
        })
        ;

        services.AddSwaggerGen(c =>
        {
            c.DocInclusionPredicate((docName, apiDesc) => apiDesc.GroupName == docName);

            c.MapNodaTimeTypes();

            c.OperationFilter<SupportFlaggedEnums>();
            c.OperationFilter<SwaggerDefaultValues>();
            c.OperationFilter<FixODataMediaTypeOnNonOData>();
            c.OperationFilter<PrettifyOperationIdOperationFilter>();


            c.UseOneOfForPolymorphism();
            c.UseAllOfForInheritance();
            c.UseAllOfToExtendReferenceSchemas();

            c.CustomOperationIds(x => x.HttpMethod + " " + x.RelativePath);
            c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

            c.OperationFilter<DefaultResponsesOperationFilter>();

            c.IncludeXmlCommentsForAssembly(this.GetType().Assembly);

            c.CustomSchemaIds((type) => ReflectionHelper.GetCSTypeName(type).Replace($"{type.Namespace}.", @"", StringComparison.Ordinal));
            c.SelectSubTypesUsing(t =>
            {
                if (t.IsGenericTypeDefinition) return Enumerable.Empty<Type>();
                return t.Assembly.GetTypes()
                     .Where(subType => subType.IsSubclassOf(t) && !subType.IsGenericTypeDefinition);
            });
            c.SupportNonNullableReferenceTypes();

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
            c.EnableTryItOutByDefault();
        });

        if (UseNewtonsoftJson)
        {
            mvcBuilder.AddNewtonsoftJson(s =>
            {
                s.SerializerSettings.ConfigureArkDefaults();
            });
            mvcBuilder.AddODataNewtonsoftJson();
            services.AddSwaggerGenNewtonsoftSupport();
        }
        else // STJ
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

        services.AddCors(c =>
        {
            c.AddDefaultPolicy(p => p
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("*")
                .SetIsOriginAllowed(_ => true));
        });
    }

    private void _integrateSimpleInjectorContainer(IServiceCollection services)
    {
        services.AddSimpleInjector(Container, o =>
        {
            o.AddAspNetCore().AddControllerActivation();

            Container.Options.ContainerLocking += (s, e) =>
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
        app.UseArkProblemDetails();

        // the rationale not to be before UseArkProblemDetails() is to avoid compressing errors for easy of use of them.
        app.UseResponseCompression();

        app.UseRouting();

        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
            SupportedCultures = CultureInfo.GetCultures(CultureTypes.InstalledWin32Cultures | CultureTypes.NeutralCultures | CultureTypes.SpecificCultures)
        });

        app.UseCors();


        if (HostEnvironment.IsDevelopment())
        {
            app.UseODataRouteDebug();
        }

        app.UseSwagger(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });

        app.UseSwaggerUI();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseODataQueryRequest();

        app.UseEndpoints(endpoints =>
        {

            endpoints.MapArkHealthChecks();
            endpoints.MapControllers();
            endpoints.Redirect("/", "/swagger");
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