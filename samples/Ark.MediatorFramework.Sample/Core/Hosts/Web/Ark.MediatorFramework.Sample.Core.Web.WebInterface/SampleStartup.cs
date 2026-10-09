// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Core.API.JsonContext;

using Ark.MediatorFramework.Sample.Core.Web.Hosting;
using Ark.MediatorFramework.Sample.Core.Web.WebInterface.Auth;
using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.MediatorFramework.Sample.Core.Application.Messages;
using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.Compliance;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.MediatorFramework.Mcp;
using Ark.Tools.Compliance.OpenApi;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Nodatime;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.Solid;
using Ark.Tools.Solid.SimpleInjector;

using MessagePack.Resolvers;

using Scalar.AspNetCore;

using SimpleInjector;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using System.Security.Claims;
using System.Text.Json;
using System.Collections.ObjectModel;

namespace Ark.MediatorFramework.Sample.Core.Web.WebInterface;

/// <summary>
/// Selects the sample API assembly for generated Minimal API endpoint discovery.
/// </summary>
[ArkGenerateMinimalApiForAssembly(typeof(Book_CreateRequest.V1))]
public partial class SampleEndpointContext
{
}

/// <summary>Selects the sample API assembly for generated MCP tool discovery.</summary>
[ArkGenerateMcpToolsForAssembly(typeof(Book_CreateRequest.V1))]
public partial class SampleMcpHostContext
{
}

/// <summary>
/// Shared ASP.NET Core pipeline configuration used both by <c>Program</c> and the self-tests,
/// so the exact same wiring is exercised under test. This hosting layer is where the selected
/// requests/queries are exposed as endpoints.
/// </summary>
public sealed class SampleStartup
{
    private readonly Container _container;
    private readonly IMessagingTransport _transport;
    private readonly IMessagingDataBus _dataBus;
    private readonly IMessagingTransportManagement? _resourceManagement;
    private readonly ArkOpenApiSecuritySettings _openApiSecurity;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    /// <summary>Initializes a new instance of the <see cref="SampleStartup"/> class.</summary>
    /// <param name="container">The application dependency injection container.</param>
    /// <param name="transport">The messaging transport.</param>
    /// <param name="dataBus">The claim-check DataBus.</param>
    /// <param name="resourceManagement">
    /// Provisions the producer's resources; <see langword="null"/> when the transport manages its own.
    /// </param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configuration">The application configuration.</param>
    public SampleStartup(
        Container container,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        IMessagingTransportManagement? resourceManagement,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        _container = container;
        _transport = transport;
        _dataBus = dataBus;
        _resourceManagement = resourceManagement;
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

    /// <summary>Registers the services the generated endpoints depend on.</summary>
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
            options.CrossWireContainer = static (container, serviceProvider) =>
            {
                container.RegisterInstance(serviceProvider.GetRequiredService<IHttpContextAccessor>());
                container.RegisterSingleton<IContextProvider<ClaimsPrincipal>, HttpUserContextProvider>();
                WebHosting.BridgeBus(container, () => serviceProvider);
            };
        });
        services.AddArkMinimalApiSecurity();

        services.AddRouting();
        services.AddControllers();

        var messagePackResolver = CompositeResolver.Create(
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
        WebHosting.AddParticipant<SampleMessagingApiParticipant>(
            services, _container, _transport, _dataBus, _resourceManagement, receiver: false);
        services.AddMcpServer()
            .WithHttpTransport()
            .WithArkMcpTools<SampleMcpHostContext>();

        // RFC 7807 ProblemDetails: map semantic domain exceptions consistently across hosts.
        services.AddArkProblemDetailsExceptionHandler();
        RuntimeTypeModel.Default.AddNodaTimeSurrogates();
        services.AddCodeFirstGrpc(static options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
        services.AddCodeFirstGrpcReflection();

        // OpenAPI: one document per API version. The generator tags expanded versioned routes
        // with their concrete group name ("v1"/"v2").
        services.AddOpenApi("v1", _configureOpenApi);
        services.AddOpenApi("v2", _configureOpenApi);
    }

    /// <summary>Builds the request pipeline and maps the exposed endpoints.</summary>
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
            endpoints.MapMcp("/mcp/v{version}").RequireAuthorization();
            endpoints.MapArkGrpcServicesFromAssembly<Book_CreateRequest.V1>();
            endpoints.MapCodeFirstGrpcReflectionService().AllowAnonymous();
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
            .AddArkComplianceSchemas()
            .AddArkServerSetProperties()
            .AddArkXmlDocumentation()
            .AddArkOAuthSecurity(_openApiSecurity)
            .AddArkPolymorphism<BookEdition, BookEditionKind>(
                "kind",
                (BookEditionKind.Print, typeof(PrintBookEdition)),
                (BookEditionKind.Digital, typeof(DigitalBookEdition)));
    }
}
