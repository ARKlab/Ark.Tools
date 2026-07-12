// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using SimpleInjector;

using Ark.Tools.Solid;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Minimal API helpers for mapping multipart attachment uploads.</summary>
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
    public static RouteHandlerBuilder MapArkAttachmentUpload<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<IArkAttachment, TRequest> factory)
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

            return TypedResults.Ok(response);
        });
    }
}
