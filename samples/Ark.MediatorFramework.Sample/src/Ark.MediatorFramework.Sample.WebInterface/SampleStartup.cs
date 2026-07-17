// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.MediatorFramework.Sample.WebInterface.Auth;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.AspNetCore.ProblemDetails;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.AspNetCore.MessagePackFormatter;

using MessagePack.Resolvers;

using Scalar.AspNetCore;

using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

using SimpleInjector;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using System.Text.Json;
using System.Collections.ObjectModel;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>
/// Shared ASP.NET Core pipeline configuration used both by <c>Program</c> and the self-tests,
/// so the exact same wiring is exercised under test. This hosting layer is where the selected
/// requests/queries are exposed as endpoints.
/// </summary>
public sealed class SampleStartup
{
    private readonly Container _container;
    private readonly ArkOpenApiSecuritySettings _openApiSecurity;
    private readonly IConfiguration _configuration;
    private readonly bool _configureFallbackPolicy;

    /// <summary>Initializes a new instance of the <see cref="SampleStartup"/> class.</summary>
    /// <param name="container">The application dependency injection container.</param>
    /// <param name="configuration">Optional application configuration.</param>
    /// <param name="configureFallbackPolicy">Whether to configure the defense-in-depth fallback policy.</param>
    public SampleStartup(
        Container container,
        IConfiguration? configuration = null,
        bool configureFallbackPolicy = true)
    {
        _container = container;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _configureFallbackPolicy = configureFallbackPolicy;
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

        if (_configuration.GetSection("EntraId").Exists()
            || _configuration.GetSection("AzureAdB2C").Exists())
        {
            services.ConfigureAuthentication(_configuration);
        }
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            if (_configureFallbackPolicy)
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        });

        services.AddSimpleInjector(_container, options =>
        {
            options.AddAspNetCore();
            _container.Options.ContainerLocking += (_, _) =>
            {
                _container.RegisterInstance(
                    options.ApplicationServices.GetRequiredService<IHttpContextAccessor>());
            };
        });
        services.AddHttpContextAccessor();
        services.AddSingleton<IHostedService>(_ => new SampleBusHostedService(_container));
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
            var contextOptions = new JsonSerializerOptions().ConfigureArkDefaults();
            var context = new SampleJsonSerializerContext(contextOptions);
            options.SerializerOptions.ConfigureArkDefaults();
            options.SerializerOptions.TypeInfoResolver = context;
        });

        // RFC 7807 ProblemDetails: map semantic domain exceptions consistently across hosts.
        services.AddArkProblemDetailsExceptionHandler();
        RuntimeTypeModel.Default.AddNodaTimeSurrogates();
        services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());
        services.AddCodeFirstGrpcReflection();

        // OpenAPI: one document per API version. The generator tags expanded versioned routes
        // with their concrete group name ("v1"/"v2").
        services.AddOpenApi("v1", ConfigureOpenApi);
        services.AddOpenApi("v2", ConfigureOpenApi);
    }

    /// <summary>Builds the request pipeline and maps the exposed endpoints.</summary>
    public void Configure(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Outermost middleware: map unhandled domain exceptions to RFC 7807 ProblemDetails responses.
        app.UseExceptionHandler();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseSimpleInjector(_container);

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Mediator API v1");
            options.SwaggerEndpoint("/openapi/v2.json", "Mediator API v2");
        });

        app.UseEndpoints(endpoints =>
        {
            // Source-generated endpoints for the selected [HttpEndpoint] contracts.
            endpoints.MapArkEndpoints<global::Ark.MediatorFramework.Sample.Application.RefreshGreetingCommand>();
            endpoints.MapArkGrpcServices<global::Ark.MediatorFramework.Sample.Application.RefreshGreetingCommand>();
            endpoints.MapGrpcService<DocumentsGrpcService>();
            endpoints.MapCodeFirstGrpcReflectionService().AllowAnonymous();
            endpoints.MapControllers();

            // Serves the generated OpenAPI documents at /openapi/{documentName}.json.
            endpoints.MapOpenApi().AllowAnonymous();
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

    private void ConfigureOpenApi(Microsoft.AspNetCore.OpenApi.OpenApiOptions options)
    {
        options
            .AddArkNodaTimeSchemas()
            .AddArkServerSetProperties()
            .AddArkOAuthSecurity(_openApiSecurity)
            .AddArkPolymorphism<Shape, ShapeKind>(
                "kind",
                (ShapeKind.Circle, typeof(Circle)),
                (ShapeKind.Square, typeof(Square)));
    }
}
