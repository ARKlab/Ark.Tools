// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Generated;
using Ark.MediatorFramework.Sample.Application;

using Ark.Tools.Solid;


using SimpleInjector;
using SimpleInjector.Lifestyles;

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
    }

    /// <summary>Builds the request pipeline and maps the exposed endpoints.</summary>
    public void Configure(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            // Source-generated endpoints for the selected [ArkEndpoint] contracts.
            endpoints.MapArkEndpoints();

            // Hand-written multipart endpoint mapping IFormFile -> IArkAttachment.
            endpoints.MapPost("/api/v1/greeting-cards", _uploadGreetingCard);
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
        await using var scope = AsyncScopedLifestyle.BeginScope(container);
        var handler = container.GetInstance<IRequestHandler<UploadGreetingCardRequest, UploadResponse>>();
        var result = await handler
            .ExecuteAsync(new UploadGreetingCardRequest { Attachment = attachment }, cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(result);
    }
}
