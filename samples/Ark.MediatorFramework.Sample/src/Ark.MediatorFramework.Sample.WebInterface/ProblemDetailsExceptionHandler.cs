// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ark.MediatorFramework.Sample.WebInterface;

/// <summary>
/// Maps the semantic domain exceptions thrown by the pure handlers onto RFC 7807
/// <see cref="ProblemDetails"/> responses for the Minimal API transport: <see cref="EntityNotFoundException"/>
/// becomes <c>404</c> and <see cref="ValidationException"/> becomes <c>400</c> with the field violations
/// packed into the <c>errors</c> extension. The handlers never format transport errors themselves.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    /// <summary>Initializes a new instance of the <see cref="ProblemDetailsExceptionHandler"/> class.</summary>
    public ProblemDetailsExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Entity not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
        };

        if (exception is ValidationException validation)
        {
            problemDetails.Extensions["errors"] = validation.Errors
                .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray(), StringComparer.Ordinal);
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        }).ConfigureAwait(false);
    }
}
