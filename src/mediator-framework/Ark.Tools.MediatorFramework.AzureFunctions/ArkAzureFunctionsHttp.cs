// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using HealthChecks.UI.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Provides HTTP file and streaming operations for generated Functions.</summary>
public static class ArkAzureFunctionsHttp
{
    /// <summary>Enforces the configured whole-request body size before the body is read.</summary>
    /// <param name="request">The current request.</param>
    /// <param name="maxRequestBodySizeBytes">The maximum request body size in bytes.</param>
    /// <returns>A 413 result when the declared or enforced size exceeds the limit, otherwise <see langword="null"/>.</returns>
    public static IResult? EnforceMaxRequestBodySize(HttpRequest request, long maxRequestBodySizeBytes)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ContentLength is { } declared && declared > maxRequestBodySizeBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        var feature = request.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = maxRequestBodySizeBytes;

        return null;
    }

    /// <summary>Reads uploaded files as transport-neutral attachments.</summary>
    /// <param name="request">The current request.</param>
    /// <param name="maxFileCount">The maximum number of files, or zero for unlimited.</param>
    /// <param name="allowedContentTypes">The allowed content types, or an empty collection for all types.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The uploaded attachments in form order.</returns>
    public static async Task<IReadOnlyList<IArkAttachment>> ReadAttachmentsAsync(
        HttpRequest request,
        int maxFileCount,
        IReadOnlyCollection<string> allowedContentTypes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(allowedContentTypes);

        if (!request.HasFormContentType)
            throw new InvalidDataException("A multipart/form-data request is required.");

        var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        if (maxFileCount > 0 && form.Files.Count > maxFileCount)
            throw new InvalidDataException("The uploaded file count exceeds the configured limit.");

        if (allowedContentTypes.Count > 0
            && form.Files.Any(file => !allowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase)))
            throw new NotSupportedException("An uploaded file content type is not allowed.");

        return form.Files
            .Select(static file => (IArkAttachment)new ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream))
            .ToArray();
    }

    /// <summary>Copies an attachment to the HTTP response and disposes its source stream.</summary>
    /// <param name="response">The current response.</param>
    /// <param name="attachment">The attachment to write.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    public static async Task WriteAttachmentAsync(
        HttpResponse response,
        IArkAttachment attachment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(attachment);

        response.ContentType = attachment.ContentType;
        response.Headers.ContentDisposition =
            "attachment; filename=\"" + ArkAttachmentName.Sanitize(attachment.Name).Replace("\"", string.Empty, StringComparison.Ordinal) + "\"";
        var stream = attachment.OpenRead();
        await using (stream.ConfigureAwait(false))
        {
            await stream.CopyToAsync(response.Body, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Writes an async sequence as a JSON array without buffering the sequence.</summary>
    /// <typeparam name="T">The streamed element type.</typeparam>
    /// <param name="response">The current response.</param>
    /// <param name="items">The response sequence.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The generated endpoint preserves the statically selected response element type.")]
    public static async Task WriteJsonStreamAsync<T>(
        HttpResponse response,
        IAsyncEnumerable<T> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(items);

        response.ContentType = "application/json; charset=utf-8";
        await System.Text.Json.JsonSerializer.SerializeAsync(
            response.Body,
            items,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Executes registered health checks and returns their HTTP status result.</summary>
    /// <param name="healthChecks">The health-check service.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>An HTTP result representing the health-check status.</returns>
    public static async Task<IResult> CheckHealthAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(healthChecks);

        var report = await healthChecks.CheckHealthAsync(cancellationToken).ConfigureAwait(false);

        return new DelegateResult(async httpContext =>
        {
            httpContext.Response.StatusCode = report.Status == HealthStatus.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;
            httpContext.Response.Headers.CacheControl = "no-store, no-cache";
            httpContext.Response.Headers.Pragma = "no-cache";
            httpContext.Response.Headers.Expires = "Thu, 01 Jan 1970 00:00:00 GMT";
            await UIResponseWriter.WriteHealthCheckUIResponseNoExceptionDetails(httpContext, report).ConfigureAwait(false);
        });
    }

    private sealed class DelegateResult : IResult
    {
        private readonly Func<HttpContext, Task> _writer;

        public DelegateResult(Func<HttpContext, Task> writer)
        {
            _writer = writer;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            await _writer(httpContext).ConfigureAwait(false);
        }
    }
}
