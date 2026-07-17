// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.AspNetCore.ProblemDetails;

/// <summary>Writes mapped exceptions as RFC 7807 responses for Minimal API hosts.</summary>
public sealed class ArkProblemDetailsExceptionHandler : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = ExceptionProblemDetailsMapper.Map(exception);
        problemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id
            ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            "application/problem+json",
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}

/// <summary>Registers Ark's transport-neutral exception mapping for Minimal API hosts.</summary>
public static class ArkProblemDetailsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the exception handler and ASP.NET Core ProblemDetails services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkProblemDetailsExceptionHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddProblemDetails();
        services.AddExceptionHandler<ArkProblemDetailsExceptionHandler>();
        return services;
    }
}
