// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.Solid;
using Ark.Tools.Nodatime.Protobuf;
using Ark.Tools.AspNetCore.MessagePackFormatter;

using MessagePack.Resolvers;

using Hellang.Middleware.ProblemDetails;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

using SimpleInjector;
using SimpleInjector.Lifestyles;

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

        // The generated endpoints resolve the SimpleInjector container from RequestServices;
        // HttpContextAccessor lets the SimpleInjector-side user context read HttpContext.User.
        services.AddSingleton(_container);
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

        // The SimpleInjector async scope is established once for the whole request, in the pipeline:
        // every endpoint (and any other middleware) resolves from this ambient scope, so the scope is
        // never re-opened per endpoint.
        app.Use(async (context, next) =>
        {
            await using var scope = AsyncScopedLifestyle.BeginScope(_container).ConfigureAwait(false);
            await next().ConfigureAwait(false);
        });

        app.UseEndpoints(endpoints =>
        {
            // Source-generated endpoints for the selected [HttpEndpoint] contracts.
            endpoints.MapArkEndpoints();
            endpoints.MapArkGrpcServices();
            endpoints.MapGrpcService<DocumentsGrpcService>();
            endpoints.MapControllers();

            // Hand-written multipart endpoint mapping IFormFile -> IArkAttachment.
            endpoints.MapPost("/api/v1/greeting-cards", _uploadGreetingCard);

            // Serves the generated OpenAPI documents at /openapi/{documentName}.json.
            endpoints.MapOpenApi();
        });
    }

    private static async Task<IResult> _uploadGreetingCard(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files["file"];
        if (file is null)
            return Results.BadRequest("Missing 'file' part.");

        var attachment = new ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream);

        var container = httpContext.RequestServices.GetRequiredService<Container>();
        var handler = container.GetInstance<IRequestHandler<UploadGreetingCardRequest, UploadResponse>>();
        var result = await handler
            .ExecuteAsync(new UploadGreetingCardRequest { Attachment = attachment }, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    private static void ConfigureOpenApi(OpenApiOptions options)
    {
        options.AddSchemaTransformer(async (schema, context, cancellationToken) =>
        {
            var format = context.JsonTypeInfo.Type == typeof(NodaTime.LocalDate) ? "date"
                : context.JsonTypeInfo.Type == typeof(NodaTime.LocalDateTime) ? "local-date-time"
                : context.JsonTypeInfo.Type == typeof(NodaTime.OffsetDateTime) ? "date-time"
                : context.JsonTypeInfo.Type == typeof(NodaTime.Period) ? "nodatime-period"
                : null;

            if (format is not null)
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = format;
            }

            if (context.JsonTypeInfo.Type == typeof(Shape))
            {
                var document = context.Document
                    ?? throw new InvalidOperationException("OpenAPI schema transformer requires a document.");
                var circleSchema = await context.GetOrCreateSchemaAsync(
                    typeof(Circle),
                    null,
                    cancellationToken).ConfigureAwait(false);
                var squareSchema = await context.GetOrCreateSchemaAsync(
                    typeof(Square),
                    null,
                    cancellationToken).ConfigureAwait(false);

                document.AddComponent(nameof(Circle), circleSchema);
                document.AddComponent(nameof(Square), squareSchema);

                var circleReference = new OpenApiSchemaReference(nameof(Circle), document);
                var squareReference = new OpenApiSchemaReference(nameof(Square), document);
                schema.OneOf = [circleReference, squareReference];
                schema.Discriminator = new OpenApiDiscriminator
                {
                    PropertyName = "kind",
                    Mapping = new Dictionary<string, OpenApiSchemaReference>(StringComparer.Ordinal)
                    {
                        [nameof(ShapeKind.Circle)] = circleReference,
                        [nameof(ShapeKind.Square)] = squareReference,
                    },
                };
            }
        });
    }
}
