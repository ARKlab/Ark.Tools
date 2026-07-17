// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
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
        httpContext.Response.ContentType = "application/problem+json";
        await System.Text.Json.JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            problemDetails,
            ProblemDetailsJsonSerializerContext.Default.ProblemDetails,
            cancellationToken).ConfigureAwait(false);
        return true;
    }
}

internal sealed class ArkProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ArkProblemDetailsExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, ArkProblemDetailsExceptionHandler handler)
    {
        try
        {
            await _next(httpContext).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await handler.TryHandleAsync(httpContext, exception, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}

    [System.Text.Json.Serialization.JsonSourceGenerationOptions(
        PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string[]>))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, object?>))]
    internal sealed partial class ProblemDetailsJsonSerializerContext : System.Text.Json.Serialization.JsonSerializerContext
    {
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
        services.AddSingleton<ArkProblemDetailsExceptionHandler>();
        services.AddExceptionHandler<ArkProblemDetailsExceptionHandler>();
        return services;
    }

    /// <summary>Activates Ark's exception handler in the ASP.NET Core request pipeline.</summary>
    /// <param name="application">The application builder.</param>
    /// <returns>The same application builder.</returns>
    public static IApplicationBuilder UseArkProblemDetailsExceptionHandler(this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return application.UseMiddleware<ArkProblemDetailsExceptionMiddleware>();
    }
}
