// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Solid;


using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Text.Json;

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

        // Minimal API JSON: the Ark System.Text.Json defaults (camelCase, NodaTime, enum-as-member)
        // so the polymorphic [JsonConverter]-annotated contracts round-trip over the wire.
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.ConfigureArkDefaults());

        // RFC 7807 ProblemDetails: the exception handler maps semantic domain exceptions
        // (EntityNotFoundException -> 404, ValidationException -> 400 + field violations).
        services.AddProblemDetails();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

        // OpenAPI: one document per API version. Endpoints are partitioned by the group name the
        // generator infers from the route template ("v1"/"v2"); ungrouped endpoints appear in both.
        services.AddOpenApi("v1");
        services.AddOpenApi("v2");
    }

    /// <summary>Builds the request pipeline and maps the exposed endpoints.</summary>
    public void Configure(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Outermost middleware: map unhandled domain exceptions to RFC 7807 ProblemDetails responses.
        app.UseExceptionHandler();

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
}
