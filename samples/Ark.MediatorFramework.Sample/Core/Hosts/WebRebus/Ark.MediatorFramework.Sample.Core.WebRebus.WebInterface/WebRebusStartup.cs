// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.API.JsonContext;
using Ark.MediatorFramework.Sample.Core.Application;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface.Auth;
using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.Compliance;
using Ark.Tools.Compliance.OpenApi;
using Ark.Tools.MediatorFramework.Generated;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.MediatorFramework.Rebus;
using Ark.Tools.NLog;
using Ark.Tools.Nodatime;
using Ark.Tools.Rebus;
using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;

using MessagePack.Resolvers;

using Scalar.AspNetCore;

using SimpleInjector;

using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface;

/// <summary>Generated Rebus one-way client for the Api participant.</summary>
[ArkRebusHost(typeof(SampleMessagingApiParticipant))]
public sealed partial class ApiRebusHost;

/// <summary>Selects the sample API assembly for generated Minimal API endpoint discovery.</summary>
[ArkGenerateMinimalApiForAssembly(typeof(Book_CreateRequest.V1))]
public partial class SampleEndpointContext
{
}

/// <summary>
/// ASP.NET Core composition of the WebRebus WebInterface, shared by <c>Program</c> and the tests so the
/// same wiring runs under test: generated Minimal API endpoints over a container whose Rebus one-way
/// client is configured by <c>RebusHosting</c>.
/// </summary>
public sealed class WebRebusStartup
{
    private readonly Container _container;
    private readonly ArkOpenApiSecuritySettings _openApiSecurity;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    /// <summary>Initializes a new instance of the <see cref="WebRebusStartup"/> class.</summary>
    /// <param name="container">The application container, with Rebus already configured.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configuration">The application configuration.</param>
    public WebRebusStartup(
        Container container,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        _container = container;
        _configuration = configuration;
        _environment = environment;
        var instance = _configuration["EntraId:Instance"]!;
        var tenantId = _configuration["EntraId:TenantId"]!;
        var clientId = _configuration["EntraId:ClientId"]!;
        var authority = $"{instance}/{tenantId}";
        _openApiSecurity = new ArkOpenApiSecuritySettings(
            new Uri($"{authority}/oauth2/v2.0/authorize"),
            new Uri($"{authority}/oauth2/v2.0/token"),
            new Uri($"{authority}/v2.0/.well-known/openid-configuration"),
            clientId,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["openid"] = "Sign in",
                [$"api://{clientId}/access_as_user"] = "Access the mediator API",
            }));
    }

    /// <summary>Applies the host registrations (logging, telemetry, services) to a web application builder.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="container">The application container, with Rebus already configured.</param>
    /// <returns>The startup used to complete the request pipeline.</returns>
    public static WebRebusStartup Create(WebApplicationBuilder builder, Container container)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseArkMinimalApiStartupDiagnostics();
        builder.Host.ConfigureNLog("Ark.MediatorFramework.Sample.Core.WebRebus.WebInterface");
        builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration);
        builder.Services.AddOpenTelemetry()
            .WithTracing(static tracing => tracing.AddSource(SampleTelemetry.ActivitySourceName));
        var startup = new WebRebusStartup(container, builder.Environment, builder.Configuration);
        startup.ConfigureServices(builder.Services);
        return startup;
    }

    /// <summary>Registers the services the generated endpoints depend on.</summary>
    /// <param name="services">The service collection.</param>
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddArkRedaction();
        NodaTimeConverter.Register();

        if (_configuration.GetSection("EntraId").Exists()
            || _configuration.GetSection("AzureAdB2C").Exists())
        {
            services.ConfigureAuthentication(_configuration, _environment);
        }
        services.AddArkSolidProcessors(_container);
        services.AddArkMinimalApiHost(_container, static options =>
        {
            // The Rebus one-way client flows this HTTP user into the message headers.
            options.CrossWireContainer = static (container, serviceProvider) =>
            {
                container.RegisterInstance(serviceProvider.GetRequiredService<IHttpContextAccessor>());
                container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, HttpUserContextProvider>();
            };
            options.OnContainerVerified = static container => container.StartBus();
        });
        services.AddArkMinimalApiSecurity();

        services.AddRouting();
        services.AddControllers();

        // The generated endpoints negotiate MessagePack for the contracts that declare it and
        // validate their formatters at startup.
        var messagePackResolver = CompositeResolver.Create(
            SampleMessagePackFormatters.Resolver,
            MessagePack.NodaTime.NodatimeResolver.Instance,
            DynamicEnumAsStringResolver.Instance,
            StandardResolver.Instance);
        services.AddMessagePackFormatter(messagePackResolver);

        // Minimal API JSON: compose the source-generated application metadata with the Ark
        // defaults (camelCase, NodaTime, enum-as-member).
        services.ConfigureHttpJsonOptions(static options =>
        {
            var context = new SampleApiJsonSerializerContext(
                new JsonSerializerOptions
                {
                    RespectNullableAnnotations = true,
                    RespectRequiredConstructorParameters = true
                }.ConfigureArkDefaults());
            var applicationContext = new ApplicationJsonSerializerContext(
                new JsonSerializerOptions
                {
                    RespectNullableAnnotations = true,
                    RespectRequiredConstructorParameters = true
                }.ConfigureArkDefaults());
            options.SerializerOptions.ConfigureArkDefaults();
            options.SerializerOptions.TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                context,
                applicationContext,
                new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
        });

        // RFC 7807 ProblemDetails: map semantic domain exceptions consistently across hosts.
        services.AddArkProblemDetailsExceptionHandler();

        // OpenAPI: one document per API version. The generator tags expanded versioned routes
        // with their concrete group name ("v1"/"v2").
        services.AddOpenApi("v1", _configureOpenApi);
        services.AddOpenApi("v2", _configureOpenApi);
    }

    /// <summary>Builds the request pipeline and maps the exposed endpoints.</summary>
    /// <param name="app">The application builder.</param>
    public void Configure(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Outermost middleware: apply security headers before any response is written.
        app.UseArkMinimalApiSecurity();

        // Map unhandled domain exceptions to RFC 7807 ProblemDetails responses.
        app.UseArkProblemDetailsExceptionHandler();

        app.UseArkMinimalApiHost(_container);

        app.UseSwaggerUI(static options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Mediator API v1");
            options.SwaggerEndpoint("/openapi/v2.json", "Mediator API v2");
        });

        app.UseEndpoints(endpoints =>
        {
            // Source-generated endpoints for the selected [HttpEndpoint] contracts.
            endpoints.MapArkEndpoints<SampleEndpointContext>(
                versionPrefix: "/api/v{version}");
            endpoints.MapArkMinimalApiHost();
            endpoints.MapControllers();

            // Serves generated JSON and YAML documents at /openapi/{documentName}.{json|yaml}.
            endpoints.MapOpenApi().AllowAnonymous();
            endpoints.MapOpenApi("/openapi/{documentName}.yaml").AllowAnonymous();
            endpoints.MapScalarApiReference(options =>
            {
                options.AddAuthorizationCodeFlow("oauth2", flow => flow
                    .WithClientId(_openApiSecurity.ClientId)
                    .WithAuthorizationUrl(_openApiSecurity.AuthorizationUrl.ToString())
                    .WithTokenUrl(_openApiSecurity.TokenUrl.ToString())
                    .WithPkce(Pkce.Sha256));
            }).AllowAnonymous();
        });
    }

    private void _configureOpenApi(Microsoft.AspNetCore.OpenApi.OpenApiOptions options)
    {
        options
            .AddArkTypeConverterValueSchemas()
            .AddArkNodaTimeSchemas()
            .AddArkValueObjectSchemas()
            .AddArkComplianceSchemas()
            .AddSensitiveValueSchema<UserId>(ArkDataClassifications.Pseudonymous, "user-0001")
            .AddArkServerSetProperties()
            .AddArkXmlDocumentation()
            .AddArkOAuthSecurity(_openApiSecurity)
            .AddArkPolymorphism<BookEdition, BookEditionKind>(
                "kind",
                (BookEditionKind.Print, typeof(PrintBookEdition)),
                (BookEditionKind.Digital, typeof(DigitalBookEdition)));
    }
}
