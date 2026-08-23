// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.API.JsonContext;

using Ark.MediatorFramework.Sample.WebInterface.Auth;
using Ark.MediatorFramework.Sample.Application.JsonContext;
using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.AspNetCore.MinimalApi;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.MediatorFramework.Mcp;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Rebus;
using Ark.Tools.Nodatime;
using Ark.Tools.Nodatime.Protobuf;

using MessagePack.Resolvers;

using Scalar.AspNetCore;

using Rebus.Transport.InMem;

using SimpleInjector;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using System.Text.Json;
using System.Collections.ObjectModel;

namespace Ark.MediatorFramework.Sample.WebInterface;

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
    private readonly InMemNetwork _network;
    private readonly bool _useSqlStore;
    private readonly string? _connectionString;
    private readonly ISampleDataContextFactory? _sharedDataContextFactory;
    private readonly ArkOpenApiSecuritySettings _openApiSecurity;
    private readonly IConfiguration _configuration;
    private readonly bool _configureFallbackPolicy;
    private readonly IHostEnvironment _environment;

    /// <summary>Initializes a new instance of the <see cref="SampleStartup"/> class.</summary>
    /// <param name="container">The application dependency injection container.</param>
    /// <param name="network">The in-memory Rebus transport network.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configuration">Optional application configuration.</param>
    /// <param name="useSqlStore">Whether the processor should use SQL persistence.</param>
    /// <param name="connectionString">Optional SQL Server connection string for the processor.</param>
    /// <param name="configureFallbackPolicy">Whether to configure the defense-in-depth fallback policy.</param>
    /// <param name="sharedDataContextFactory">
    /// Optional in-memory context factory shared between the API and processor containers so both operate
    /// on the same data without a database. <see langword="null"/> when <paramref name="useSqlStore"/>
    /// is <see langword="true"/> (the SQL database is the shared state).
    /// </param>
    public SampleStartup(
        Container container,
        InMemNetwork network,
        IHostEnvironment environment,
        IConfiguration? configuration = null,
        bool useSqlStore = true,
        string? connectionString = null,
        bool configureFallbackPolicy = true,
        ISampleDataContextFactory? sharedDataContextFactory = null)
    {
        _container = container;
        _network = network;
        _useSqlStore = useSqlStore;
        _connectionString = connectionString;
        _sharedDataContextFactory = sharedDataContextFactory;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _configureFallbackPolicy = configureFallbackPolicy;
        ArgumentNullException.ThrowIfNull(environment);
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
        NodaTimeConverter.Register();

        if (_configuration.GetSection("EntraId").Exists()
            || _configuration.GetSection("AzureAdB2C").Exists())
        {
            services.ConfigureAuthentication(_configuration, _environment);
        }
        services.AddArkMinimalApiHost(_container, options =>
        {
            options.RequireAuthenticatedUser = _configureFallbackPolicy;
            options.CrossWireContainer = (container, serviceProvider) =>
                container.RegisterInstance(serviceProvider.GetRequiredService<IHttpContextAccessor>());
            // Started right after verification, while the host is starting and before the
            // server accepts requests.
            options.OnContainerVerified = container => container.StartBus();
        });
        services.AddArkMinimalApiSecurity();

        // The InMemNetwork is registered in Microsoft DI so both the API container and the
        // processor hosted service can access it without depending on each other.
        services.AddSingleton(_network);

        // API container disposal. Host composition verifies the container and starts the bus;
        // this service solely owns disposal during host shutdown.
        services.AddSingleton<IHostedService>(_ => new SampleApiContainerHostedService(_container));

        // Processor container is built and managed independently from the API container;
        // the only shared resource is InMemNetwork resolved from Microsoft DI.
        services.AddSingleton<IHostedService>(sp => new SampleBusHostedService(
            sp.GetRequiredService<InMemNetwork>(),
            useSqlStore: _useSqlStore,
            connectionString: _connectionString,
            sharedDataContextFactory: _sharedDataContextFactory));

        services.AddRouting();
        services.AddControllers();

        var messagePackResolver = CompositeResolver.Create(
            MessagePack.NodaTime.NodatimeResolver.Instance,
            DynamicEnumAsStringResolver.Instance,
            StandardResolver.Instance);
        services.AddMessagePackFormatter(messagePackResolver);

        // Minimal API JSON: compose the source-generated application metadata with the Ark
        // defaults (camelCase, NodaTime, enum-as-member).
        services.ConfigureHttpJsonOptions(options =>
        {
            var context = new SampleApiJsonSerializerContext(
                new JsonSerializerOptions().ConfigureArkDefaults());
            var applicationContext = new ApplicationJsonSerializerContext(
                new JsonSerializerOptions().ConfigureArkDefaults());
            options.SerializerOptions.ConfigureArkDefaults();
            options.SerializerOptions.TypeInfoResolver = System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver.Combine(
                context,
                applicationContext,
                new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
        });
        services.AddArkMessaging();
        services.AddMcpServer()
            .WithHttpTransport()
            .WithArkMcpTools<SampleMcpHostContext>();

        // RFC 7807 ProblemDetails: map semantic domain exceptions consistently across hosts.
        services.AddArkProblemDetailsExceptionHandler();
        RuntimeTypeModel.Default.AddNodaTimeSurrogates();
        services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
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

        app.UseSwaggerUI(options =>
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
            endpoints.MapMcp("/mcp").RequireAuthorization();
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
            .AddArkServerSetProperties()
            .AddArkXmlDocumentation()
            .AddArkOAuthSecurity(_openApiSecurity)
            .AddArkPolymorphism<BookEdition, BookEditionKind>(
                "kind",
                (BookEditionKind.Print, typeof(PrintBookEdition)),
                (BookEditionKind.Digital, typeof(DigitalBookEdition)));
    }
}
