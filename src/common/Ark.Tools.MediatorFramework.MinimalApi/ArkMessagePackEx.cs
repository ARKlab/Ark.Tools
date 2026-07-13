// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using MessagePack;
using MessagePack.Resolvers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Minimal API helpers for MessagePack request and response serialization.</summary>
public static class ArkMessagePackEx
{
    private const string MessagePackMediaType = "application/x-msgpack";

    /// <summary>
    /// Maps a POST endpoint that negotiates MessagePack while retaining JSON support.
    /// </summary>
    /// <typeparam name="TRequest">The request type accepted by the handler.</typeparam>
    /// <typeparam name="TResponse">The response type returned by the handler.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="handler">The pure handler delegate.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder MapArkMessagePackPost<TRequest, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<HttpContext, TRequest, CancellationToken, Task<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        ArgumentNullException.ThrowIfNull(handler);

        return endpoints.MapPost(pattern, async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var request = IsMessagePack(context.Request.ContentType)
                ? await MessagePackSerializer.DeserializeAsync<TRequest>(
                    context.Request.Body,
                    GetOptions(context),
                    cancellationToken).ConfigureAwait(false)
                : await context.Request.ReadFromJsonAsync<TRequest>(cancellationToken).ConfigureAwait(false);

            if (request is null)
                return Results.BadRequest();

            var response = await handler(context, request, cancellationToken).ConfigureAwait(false);
            if (!PrefersMessagePack(context.Request.Headers.Accept))
                return Results.Json(response);

            var bytes = MessagePackSerializer.Serialize(response, GetOptions(context));
            return Results.Bytes(bytes, MessagePackMediaType);
        });
    }

    private static MessagePackSerializerOptions GetOptions(HttpContext context)
    {
        var resolver = context.RequestServices.GetService<IFormatterResolver>()
            ?? CompositeResolver.Create(
                MessagePack.NodaTime.NodatimeResolver.Instance,
                DynamicEnumAsStringResolver.Instance,
                StandardResolver.Instance);
        return MessagePackSerializerOptions.Standard.WithResolver(resolver);
    }

    private static bool IsMessagePack(string? contentType)
        => contentType?.StartsWith(MessagePackMediaType, StringComparison.OrdinalIgnoreCase) == true;

    private static bool PrefersMessagePack(string? accept)
    {
        if (string.IsNullOrWhiteSpace(accept))
            return false;

        return accept.Split(',', StringSplitOptions.TrimEntries)
            .Select(static value => value.Split(';', StringSplitOptions.TrimEntries))
            .Any(static parts => string.Equals(parts[0], MessagePackMediaType, StringComparison.OrdinalIgnoreCase)
                && !parts.Skip(1).Any(static parameter => parameter.StartsWith("q=0", StringComparison.OrdinalIgnoreCase)));
    }
}
