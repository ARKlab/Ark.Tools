// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.MediatorFramework.Sample.WebInterface.Auth;
using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.AspNetCore.MessagePackFormatter;

using MessagePack.Resolvers;

using Scalar.AspNetCore;

using Hellang.Middleware.ProblemDetails;

using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;

using SimpleInjector;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using System.Text.Json;
using System.Collections.ObjectModel;

using HellangProblemDetailsOptions = Hellang.Middleware.ProblemDetails.ProblemDetailsOptions;

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

    /// <summary>Initializes a new instance of the <see cref="SampleStartup"/> class.</summary>
    public SampleStartup(Container container, IConfiguration? configuration = null)
    {
        _container = container;
        _configuration = configuration ?? new ConfigurationBuilder().Build();
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
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
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

        // RFC 7807 ProblemDetails: Hellang maps semantic domain exceptions
        // (EntityNotFoundException -> 404, ValidationException -> 400 + field violations).
        Hellang.Middleware.ProblemDetails.ProblemDetailsExtensions.AddProblemDetails(services);
        services.AddSingleton<IConfigureOptions<HellangProblemDetailsOptions>, SampleProblemDetailsOptionsSetup>();
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
        app.UseProblemDetails();

        app.UseRouting();
        app.UseAuthentication();
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal)
                || context.Request.ContentType?.StartsWith("application/grpc", StringComparison.OrdinalIgnoreCase) == true,
            branch => branch.UseAuthorization());

        app.UseSimpleInjector(_container);

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Mediator API v1");
            options.SwaggerEndpoint("/openapi/v2.json", "Mediator API v2");
        });

        app.UseEndpoints(endpoints =>
        {
            // Source-generated endpoints for the selected [HttpEndpoint] contracts.
            endpoints.MapArkEndpoints();
            endpoints.MapArkGrpcServices();
            endpoints.MapGrpcService<DocumentsGrpcService>();
            endpoints.MapCodeFirstGrpcReflectionService();
            endpoints.MapControllers();

            // Serves the generated OpenAPI documents at /openapi/{documentName}.json.
            endpoints.MapOpenApi();
            endpoints.MapScalarApiReference(options =>
            {
                options.AddAuthorizationCodeFlow("oauth2", flow => flow
                    .WithClientId(_openApiSecurity.ClientId)
                    .WithAuthorizationUrl(_openApiSecurity.AuthorizationUrl.ToString())
                    .WithTokenUrl(_openApiSecurity.TokenUrl.ToString())
                    .WithPkce(Pkce.Sha256));
            });
        });
    }

    private void ConfigureOpenApi(Microsoft.AspNetCore.OpenApi.OpenApiOptions options)
    {
        options
            .AddArkNodaTimeSchemas()
            .AddArkOAuthSecurity(_openApiSecurity)
            .AddArkPolymorphism<Shape, ShapeKind>(
                "kind",
                (ShapeKind.Circle, typeof(Circle)),
                (ShapeKind.Square, typeof(Square)));
    }
}
