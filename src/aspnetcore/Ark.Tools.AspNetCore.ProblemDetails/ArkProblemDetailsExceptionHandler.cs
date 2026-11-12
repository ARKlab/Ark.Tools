// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NLog;

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Ark.Tools.AspNetCore.ProblemDetails;

/// <summary>Writes mapped exceptions as RFC 7807 responses for Minimal API hosts.</summary>
public sealed class ArkProblemDetailsExceptionHandler : IExceptionHandler
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly bool _includeExceptionDetails;

    /// <summary>Initializes the exception handler.</summary>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="options">The exception detail options.</param>
    public ArkProblemDetailsExceptionHandler(
        IHostEnvironment? environment = null,
        IOptions<ArkProblemDetailsOptions>? options = null)
    {
        _includeExceptionDetails = environment?.IsDevelopment() == true
            || options?.Value.IncludeExceptionDetails == true;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var options = ArkProblemDetailsJson.GetOptions(httpContext.RequestServices);
        var policy = options.PropertyNamingPolicy;
        var problemDetails = ExceptionProblemDetailsMapper.Map(exception, policy);
        if (problemDetails.Status is null or >= 500)
        {
            _logger.Error(exception, CultureInfo.InvariantCulture, "Unhandled exception while processing an HTTP request.");
        }
        if (_includeExceptionDetails)
        {
            problemDetails.Detail = exception.Message;
            problemDetails.Extensions[policy?.ConvertName("stackTrace") ?? "stackTrace"] = exception.StackTrace;
        }
        var traceIdName = policy?.ConvertName("traceId") ?? "traceId";
        var traceId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions[traceIdName] = traceId;
        var typeInfo = (JsonTypeInfo<Microsoft.AspNetCore.Mvc.ProblemDetails>)options.GetTypeInfo(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails));
        byte[] body;
        try
        {
            body = JsonSerializer.SerializeToUtf8Bytes(problemDetails, typeInfo);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The details are mapped as they are. If the options cannot write them (an unknown type, a cycle,
            // a converter that throws), the client still gets the error, without the details.
            _logger.Warn(e, CultureInfo.InvariantCulture, "The ProblemDetails extensions cannot be serialized and are left out.");
            problemDetails.Extensions.Clear();
            problemDetails.Extensions[traceIdName] = traceId;
            body = JsonSerializer.SerializeToUtf8Bytes(problemDetails, typeInfo);
        }
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.Body.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        return true;
    }
}

/// <summary>Invokes the shared exception handler around the ASP.NET Core pipeline.</summary>
internal sealed class ArkProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Initializes the middleware.</summary>
    /// <param name="next">The next pipeline delegate.</param>
    public ArkProblemDetailsExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Invokes the next pipeline delegate and maps unhandled exceptions.</summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="handler">The shared exception handler.</param>
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

/// <summary>Provides the JSON options that write Ark ProblemDetails responses.</summary>
public static class ArkProblemDetailsJson
{
    // Keyed by the application's options instance, a singleton, so every request gets the same copy.
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _cache = new();

    /// <summary>
    /// Gets the JSON options for a ProblemDetails response: a read-only copy of the application's Minimal API
    /// options (<c>ConfigureHttpJsonOptions</c>), so that its converters, source-generated contexts and naming
    /// policy apply to the error details.
    /// </summary>
    /// <param name="services">The application services, or <see langword="null"/> when there are none.</param>
    /// <returns>
    /// The options; repeated calls for one application return the same instance. Without application options,
    /// the options of the built-in ProblemDetails context.
    /// </returns>
    /// <remarks>
    /// The copy has no dictionary key policy: the keys of a dictionary, such as the validation errors, are data
    /// and are written as they are. The built-in ProblemDetails context is appended after the application's own
    /// resolvers, so it only supplies metadata the application does not provide.
    /// </remarks>
    public static JsonSerializerOptions GetOptions(IServiceProvider? services)
    {
        var application = services?.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions;
        return application is null
            ? ProblemDetailsJsonSerializerContext.Default.Options
            : _cache.GetValue(application, static options => _create(options));
    }

    private static JsonSerializerOptions _create(JsonSerializerOptions application)
    {
        var options = new JsonSerializerOptions(application) { DictionaryKeyPolicy = null };
        options.TypeInfoResolverChain.Add(ProblemDetailsJsonSerializerContext.Default);
        options.MakeReadOnly();
        return options;
    }
}

/// <summary>Source-generated JSON metadata for ProblemDetails responses.</summary>
[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
#if NET9_0_OR_GREATER
    ,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true
#endif
    )]
[System.Text.Json.Serialization.JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string[]>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, object?>))]
[System.Text.Json.Serialization.JsonSerializable(typeof(JsonElement))]
[System.Text.Json.Serialization.JsonSerializable(typeof(bool))]
[System.Text.Json.Serialization.JsonSerializable(typeof(long))]
[System.Text.Json.Serialization.JsonSerializable(typeof(double))]
[System.Text.Json.Serialization.JsonSerializable(typeof(decimal))]
[System.Text.Json.Serialization.JsonSerializable(typeof(Guid))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DateTime))]
[System.Text.Json.Serialization.JsonSerializable(typeof(DateTimeOffset))]
[System.Text.Json.Serialization.JsonSerializable(typeof(string[]))]
[System.Text.Json.Serialization.JsonSerializable(typeof(List<string>))]
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
    /// <param name="configure">Optional exception detail configuration.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkProblemDetailsExceptionHandler(
        this IServiceCollection services,
        Action<ArkProblemDetailsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null)
        {
            services.Configure(configure);
        }
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
