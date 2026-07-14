// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.MinimalApi;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.AspNetCore.MessagePackFormatter;
using Ark.Tools.Rebus;

using MessagePack.Resolvers;

using Hellang.Middleware.ProblemDetails;

using Microsoft.Extensions.Options;

using SimpleInjector;

using ProtoBuf.Grpc.Server;
using ProtoBuf.Meta;

using System.Text.Json;

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

    /// <summary>Initializes a new instance of the <see cref="SampleStartup"/> class.</summary>
    public SampleStartup(Container container)
    {
        _container = container;
    }

    /// <summary>Registers the services the generated endpoints depend on.</summary>
    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSimpleInjector(_container, options => options.AddAspNetCore());
        services.AddHttpContextAccessor();
        services.AddRouting();
        services.AddControllers();

        var messagePackResolver = CompositeResolver.Create(
            MessagePack.NodaTime.NodatimeResolver.Instance,
            DynamicEnumAsStringResolver.Instance,
            StandardResolver.Instance);
        services.AddMessagePackFormatter(messagePackResolver);

        // Minimal API JSON: the Ark System.Text.Json defaults (camelCase, NodaTime, enum-as-member)
        // so the polymorphic [JsonConverter]-annotated contracts round-trip over the wire.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureArkDefaults());

        // RFC 7807 ProblemDetails: Hellang maps semantic domain exceptions
        // (EntityNotFoundException -> 404, ValidationException -> 400 + field violations).
        Hellang.Middleware.ProblemDetails.ProblemDetailsExtensions.AddProblemDetails(services);
        services.AddSingleton<IConfigureOptions<HellangProblemDetailsOptions>, SampleProblemDetailsOptionsSetup>();
        RuntimeTypeModel.Default.AddNodaTimeSurrogates();
        services.AddCodeFirstGrpc(options => options.Interceptors.Add<ArkGrpcErrorInterceptor>());

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

        app.UseSimpleInjector(_container);
        _container.Verify();
        _container.StartBus();

        app.UseEndpoints(endpoints =>
        {
            // Source-generated endpoints for the selected [HttpEndpoint] contracts.
            endpoints.MapArkEndpoints();
            endpoints.MapArkGrpcServices();
            endpoints.MapGrpcService<DocumentsGrpcService>();
            endpoints.MapControllers();

            // Serves the generated OpenAPI documents at /openapi/{documentName}.json.
            endpoints.MapOpenApi();
        });
    }

    private static void ConfigureOpenApi(Microsoft.AspNetCore.OpenApi.OpenApiOptions options)
    {
        options
            .AddArkNodaTimeSchemas()
            .AddArkPolymorphism<Shape, ShapeKind>(
                "kind",
                (ShapeKind.Circle, typeof(Circle)),
                (ShapeKind.Square, typeof(Square)));
    }
}
