// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>Minimal API helpers for MessagePack request and response serialization.</summary>
[SuppressMessage("Naming", "CA1711", Justification = "The Ex suffix is part of the public Ark extension API naming convention.")]
public static class ArkMessagePackEx
{
    private const string MessagePackMediaType = "application/x-msgpack";

    /// <summary>Reads a request using MessagePack or JSON content negotiation.</summary>
    /// <typeparam name="TRequest">The request type accepted by the handler.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The deserialized request, or <see langword="null"/> for an empty body.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The endpoint delegate's request and response types are statically supplied by the application call site.")]
    public static async Task<TRequest?> ReadRequestAsync<TRequest>(
        HttpContext context,
        CancellationToken cancellationToken)
        where TRequest : notnull
    {
        ArgumentNullException.ThrowIfNull(context);

        return IsMessagePack(context.Request.ContentType)
            ? await MessagePackSerializer.DeserializeAsync<TRequest>(
                context.Request.Body,
                GetDeserializationOptions(context),
                cancellationToken).ConfigureAwait(false)
            : await context.Request.ReadFromJsonAsync<TRequest>(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates the MessagePack formatters required by generated endpoints.</summary>
    /// <param name="services">The application service provider.</param>
    /// <param name="validators">Formatter validations generated for MessagePack contracts.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when one or more contract formatters cannot be resolved.
    /// </exception>
    public static void ValidateMessagePackContracts(
        IServiceProvider services,
        params Action<IFormatterResolver>[] validators)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(validators);

        var resolver = services.GetRequiredService<IFormatterResolver>();
        var failures = new List<string>();
        foreach (var validator in validators)
        {
            try
            {
                validator(resolver);
            }
            catch (Exception exception) when (exception is MessagePackSerializationException or InvalidOperationException)
            {
                failures.Add(exception.Message);
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "MessagePack formatter validation failed: " + string.Join("; ", failures));
    }

    /// <summary>Validates that the configured resolver has a formatter for a contract type.</summary>
    /// <typeparam name="T">The MessagePack contract type.</typeparam>
    /// <param name="resolver">The configured formatter resolver.</param>
    public static void ValidateMessagePackFormatter<T>(IFormatterResolver resolver)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _ = resolver.GetFormatterWithVerify<T>();
    }

    /// <summary>Writes a response using the client's preferred JSON or MessagePack format.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="response">The response value.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <param name="successStatusCode">The status code for a non-null response.</param>
    /// <param name="nullResultStatusCode">The status code for a null response.</param>
    /// <returns>An HTTP result using the negotiated response format.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The endpoint delegate's request and response types are statically supplied by the application call site.")]
    public static IResult WriteResponse<TResponse>(
        HttpContext context,
        TResponse response,
        CancellationToken cancellationToken,
        int successStatusCode = StatusCodes.Status200OK,
        int nullResultStatusCode = StatusCodes.Status204NoContent)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (response is null)
            return Results.StatusCode(nullResultStatusCode);

        if (!PrefersMessagePack(context.Request.Headers.Accept))
            return Results.Json(response, statusCode: successStatusCode);

        var bytes = MessagePackSerializer.Serialize(response, GetOptions(context), cancellationToken);
        context.Response.StatusCode = successStatusCode;
        return Results.Bytes(bytes, MessagePackMediaType);
    }

    private static MessagePackSerializerOptions GetOptions(HttpContext context)
    {
        var resolver = context.RequestServices.GetRequiredService<IFormatterResolver>();
        return MessagePackSerializerOptions.Standard.WithResolver(resolver);
    }

    private static MessagePackSerializerOptions GetDeserializationOptions(HttpContext context)
        => GetOptions(context).WithSecurity(MessagePackSecurity.UntrustedData);

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
