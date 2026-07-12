// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.Tools.Solid;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Minimal API helpers for mapping multipart attachment uploads.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix is part of the public Ark extension API naming convention.")]
public static class ArkMultipartEx
{
    /// <summary>
    /// Maps a multipart upload to an attachment-based request handler.
    /// </summary>
    /// <typeparam name="TRequest">The request type accepted by the handler.</typeparam>
    /// <typeparam name="TResponse">The handler response type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="factory">Creates a request from the uploaded attachment.</param>
    /// <returns>The route handler builder.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The endpoint delegate's request and response types are statically supplied by the application call site.")]
    public static RouteHandlerBuilder MapArkAttachmentUpload<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<IArkAttachment, TRequest> factory)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(factory);

        return endpoints.MapPost(pattern, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            var file = form.Files["file"];
            if (file is null)
                return Results.BadRequest("Missing 'file' part.");

            var attachment = new ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream);
            var container = context.RequestServices.GetRequiredService<Container>();
            var handler = container.GetInstance<IRequestHandler<TRequest, TResponse>>();
            var response = await handler
                .ExecuteAsync(factory(attachment), cancellationToken)
                .ConfigureAwait(false);

            return (IResult)TypedResults.Ok(response);
        });
    }
}
