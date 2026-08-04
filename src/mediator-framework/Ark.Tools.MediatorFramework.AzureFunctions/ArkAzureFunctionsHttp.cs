// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Provides HTTP file and streaming operations for generated Functions.</summary>
public static class ArkAzureFunctionsHttp
{
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
            .Select(file => (IArkAttachment)new ArkAttachment(file.FileName, file.ContentType, file.OpenReadStream))
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
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
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
        await global::System.Text.Json.JsonSerializer.SerializeAsync(
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
        return Results.StatusCode(report.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
    }
}
