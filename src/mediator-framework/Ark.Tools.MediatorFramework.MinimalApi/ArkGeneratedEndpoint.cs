// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ark.Tools.MediatorFramework.MinimalApi;

/// <summary>
/// Maps endpoints emitted by the Minimal API generator without <see cref="RequestDelegateFactory"/>.
/// </summary>
/// <remarks>
/// The ASP.NET Core Request Delegate Generator only intercepts <c>Map*</c> calls in user-written source, so it
/// never sees generated endpoints. The generator therefore emits what it would: the handler binds its parameters in
/// generated code and is mapped through <see cref="RouteHandlerServices"/>. The handler still supplies the
/// <see cref="MethodInfo"/> that ApiExplorer and OpenAPI read. Infrastructure for generated code only.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArkGeneratedEndpoint
{
    private static readonly string[] _jsonContentType = ["application/json"];

    /// <summary>Maps a generated handler with generated metadata and request binding.</summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="httpMethod">The HTTP method.</param>
    /// <param name="handler">The generated handler.</param>
    /// <param name="populateMetadata">Adds the parameter and request metadata of the handler.</param>
    /// <param name="createRequestDelegate">Creates the request delegate that binds and invokes the handler.</param>
    /// <returns>The route handler builder.</returns>
    public static RouteHandlerBuilder Map(
        IEndpointRouteBuilder endpoints,
        string pattern,
        string httpMethod,
        Delegate handler,
        Action<ParameterInfo[], EndpointBuilder> populateMetadata,
        Func<Delegate, ArkEndpointBinder, RequestDelegate> createRequestDelegate)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(populateMetadata);
        ArgumentNullException.ThrowIfNull(createRequestDelegate);

        return RouteHandlerServices.Map(
            endpoints,
            pattern,
            handler,
            [httpMethod],
            (methodInfo, options) =>
            {
                var builder = options?.EndpointBuilder ?? throw new InvalidOperationException("The endpoint builder is not available.");
                populateMetadata(methodInfo.GetParameters(), builder);
                return new RequestDelegateMetadataResult { EndpointMetadata = builder.Metadata.AsReadOnly() };
            },
            (del, options, inferredMetadata) =>
            {
                var builder = options.EndpointBuilder ?? throw new InvalidOperationException("The endpoint builder is not available.");
                var requestDelegate = createRequestDelegate(del, new ArkEndpointBinder(options, builder, del.Method));
                return new RequestDelegateResult(requestDelegate, inferredMetadata?.EndpointMetadata ?? builder.Metadata.AsReadOnly());
            },
            handler.Method);
    }

    /// <summary>Describes a bound handler parameter to ApiExplorer and OpenAPI.</summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="parameter">The handler parameter.</param>
    /// <param name="hasTryParse">Whether the value is parsed from a string.</param>
    /// <param name="isOptional">Whether the value is optional.</param>
    public static void AddParameter(EndpointBuilder builder, string name, ParameterInfo parameter, bool hasTryParse, bool isOptional)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new ParameterBindingMetadata(name, parameter, hasTryParse, isOptional));
    }

    /// <summary>Declares an inferred JSON request body, as <see cref="RequestDelegateFactory"/> does.</summary>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="bodyType">The body type.</param>
    /// <param name="isOptional">Whether the body is optional.</param>
    public static void AddJsonBody(EndpointBuilder builder, Type bodyType, bool isOptional)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Metadata.Add(new AcceptsMetadata(_jsonContentType, bodyType, isOptional));
        if (!builder.Metadata.Any(static metadata => metadata is IDisableCookieRedirectMetadata))
            builder.Metadata.Add(DisableCookieRedirectMetadata.Instance);
    }

    /// <summary>Creates a JSON result with a status code, serialized with the type information of the configured options.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns>The result.</returns>
    public static IResult Json<T>(T value, int statusCode) => new JsonResult<T>(value, statusCode);

    private sealed class ParameterBindingMetadata(string name, ParameterInfo parameterInfo, bool hasTryParse, bool isOptional) : IParameterBindingMetadata
    {
        public string Name => name;

        public bool HasTryParse => hasTryParse;

        public bool HasBindAsync => false;

        public ParameterInfo ParameterInfo => parameterInfo;

        public bool IsOptional => isOptional;
    }

    private sealed class DisableCookieRedirectMetadata : IDisableCookieRedirectMetadata
    {
        public static readonly DisableCookieRedirectMetadata Instance = new();
    }

    private sealed class JsonResult<T>(T value, int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            var options = httpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(value, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)), contentType: null, httpContext.RequestAborted).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Binds the parameters of a generated Minimal API endpoint with the semantics of
/// <see cref="RequestDelegateFactory"/>. Infrastructure for generated code only.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ArkEndpointBinder
{
    private static readonly global::NLog.Logger _logger = global::NLog.LogManager.GetCurrentClassLogger();
    private readonly bool _throwOnBadRequest;
    private readonly EndpointBuilder _builder;
    private readonly MethodInfo _method;

    internal ArkEndpointBinder(RequestDelegateFactoryOptions options, EndpointBuilder builder, MethodInfo method)
    {
        _throwOnBadRequest = options.ThrowOnBadRequest;
        _builder = builder;
        _method = method;
        var services = options.ServiceProvider ?? builder.ApplicationServices;
        JsonSerializerOptions = services.GetService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()?.Value.SerializerOptions
            ?? new Microsoft.AspNetCore.Http.Json.JsonOptions().SerializerOptions;
        JsonSerializerOptions.MakeReadOnly();
    }

    /// <summary>Gets the serializer options of the application.</summary>
    public JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>Gets whether endpoint filters are configured.</summary>
    public bool HasFilters => _builder.FilterFactories.Count > 0;

    /// <summary>Gets the invariant culture used to parse route and query values.</summary>
    public static CultureInfo Culture => CultureInfo.InvariantCulture;

    /// <summary>Parses a value through its <see cref="IParsable{TSelf}"/> implementation, including an explicit one.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The text.</param>
    /// <param name="result">The parsed value.</param>
    /// <returns><see langword="true"/> when parsing succeeds.</returns>
    public static bool TryParse<T>(string? value, [MaybeNullWhen(false)] out T result)
        where T : IParsable<T>
        => T.TryParse(value, CultureInfo.InvariantCulture, out result);

    /// <summary>Reports a required parameter missing from the request.</summary>
    /// <param name="typeName">The parameter type name.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="source">The request source.</param>
    public void RequiredParameterNotProvided(string typeName, string parameterName, string source)
    {
        if (_throwOnBadRequest)
            throw new BadHttpRequestException(string.Format(CultureInfo.InvariantCulture, "Required parameter \"{0} {1}\" was not provided from {2}.", typeName, parameterName, source));
        _logger.Debug(CultureInfo.InvariantCulture, "Required parameter \"{ParameterType} {ParameterName}\" was not provided from {Source}.", typeName, parameterName, source);
    }

    /// <summary>Reports a value that could not be converted to the parameter type.</summary>
    /// <param name="typeName">The parameter type name.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="sourceValue">The value.</param>
    public void ParameterBindingFailed(string typeName, string parameterName, string? sourceValue)
    {
        if (_throwOnBadRequest)
            throw new BadHttpRequestException(string.Format(CultureInfo.InvariantCulture, "Failed to bind parameter \"{0} {1}\" from \"{2}\".", typeName, parameterName, sourceValue));
        _logger.Debug(CultureInfo.InvariantCulture, "Failed to bind parameter \"{ParameterType} {ParameterName}\" from \"{SourceValue}\".", typeName, parameterName, _forLog(sourceValue));
    }

    // Request values are logged with line breaks escaped, so a value cannot forge log entries.
    private static string? _forLog(string? value)
        => value?.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>Reads an inferred JSON body with the semantics of <see cref="RequestDelegateFactory"/>.</summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="httpContext">The request context.</param>
    /// <param name="allowEmpty">Whether an empty body binds <see langword="null"/>.</param>
    /// <param name="typeName">The body type name.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>Whether binding succeeded, and the body. On failure the response status code is set.</returns>
    public async ValueTask<(bool Success, T? Value)> ReadJsonBodyAsync<T>(HttpContext httpContext, bool allowEmpty, string typeName, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var feature = httpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestBodyDetectionFeature>();
        T? value = default;
        var hasValue = false;
        if (feature?.CanHaveBody == true)
        {
            if (!httpContext.Request.HasJsonContentType())
            {
                if (_throwOnBadRequest)
                    throw new BadHttpRequestException(string.Format(CultureInfo.InvariantCulture, "Expected a supported JSON media type but got \"{0}\".", httpContext.Request.ContentType), StatusCodes.Status415UnsupportedMediaType);
                _logger.Debug(CultureInfo.InvariantCulture, "Expected a supported JSON media type but got \"{ContentType}\".", _forLog(httpContext.Request.ContentType) ?? "(none)");
                httpContext.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                return (false, default);
            }

            try
            {
                value = await httpContext.Request.ReadFromJsonAsync((JsonTypeInfo<T>)JsonSerializerOptions.GetTypeInfo(typeof(T)), httpContext.RequestAborted).ConfigureAwait(false);
                hasValue = value is not null;
            }
            catch (BadHttpRequestException exception)
            {
                _logger.Debug(exception, CultureInfo.InvariantCulture, "Reading the request body failed with an IOException.");
                httpContext.Response.StatusCode = exception.StatusCode;
                return (false, default);
            }
            catch (IOException exception)
            {
                _logger.Debug(exception, CultureInfo.InvariantCulture, "Reading the request body failed with an IOException.");
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return (false, default);
            }
            catch (JsonException exception)
            {
                if (_throwOnBadRequest)
                    throw new BadHttpRequestException(string.Format(CultureInfo.InvariantCulture, "Failed to read parameter \"{0} {1}\" from the request body as JSON.", typeName, parameterName), exception);
                _logger.Debug(exception, CultureInfo.InvariantCulture, "Failed to read parameter \"{ParameterType} {ParameterName}\" from the request body as JSON.", typeName, parameterName);
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                return (false, default);
            }
        }

        if (!allowEmpty && !hasValue)
        {
            if (_throwOnBadRequest)
                throw new BadHttpRequestException(string.Format(CultureInfo.InvariantCulture, "Implicit body inferred for parameter \"{0}\" but no body was provided. Did you mean to use a Service instead?", parameterName));
            _logger.Debug(CultureInfo.InvariantCulture, "Implicit body inferred for parameter \"{ParameterName}\" but no body was provided.", parameterName);
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return (false, value);
        }

        return (true, value);
    }

    /// <summary>Builds the endpoint filter pipeline around the generated handler invocation.</summary>
    /// <param name="invocation">Invokes the handler with the filter arguments.</param>
    /// <returns>The filtered invocation.</returns>
    public EndpointFilterDelegate BuildFilterPipeline(EndpointFilterDelegate invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        var context = new EndpointFilterFactoryContext
        {
            MethodInfo = _method,
            ApplicationServices = _builder.ApplicationServices,
        };
        var filtered = invocation;
        for (var i = _builder.FilterFactories.Count - 1; i >= 0; i--)
            filtered = _builder.FilterFactories[i](context, filtered);
        return filtered;
    }

    /// <summary>Runs the filter pipeline and writes its result.</summary>
    /// <param name="pipeline">The filter pipeline.</param>
    /// <param name="httpContext">The request context.</param>
    /// <param name="arguments">The bound handler arguments.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public async Task InvokeFilteredAsync(EndpointFilterDelegate pipeline, HttpContext httpContext, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(httpContext);
        var result = await pipeline(new DefaultEndpointFilterInvocationContext(httpContext, arguments)).ConfigureAwait(false);
        switch (result)
        {
            case null:
                return;
            case IResult endpointResult:
                await endpointResult.ExecuteAsync(httpContext).ConfigureAwait(false);
                return;
            case string text:
                await httpContext.Response.WriteAsync(text, httpContext.RequestAborted).ConfigureAwait(false);
                return;
            default:
                await httpContext.Response.WriteAsJsonAsync(result, JsonSerializerOptions.GetTypeInfo(result.GetType()), contentType: null, httpContext.RequestAborted).ConfigureAwait(false);
                return;
        }
    }

    /// <summary>Writes the result of an unfiltered handler invocation.</summary>
    /// <param name="result">The handler result.</param>
    /// <param name="httpContext">The request context.</param>
    /// <returns>A task that completes when the response is written.</returns>
    public static async Task ExecuteAsync(IResult? result, HttpContext httpContext)
    {
        if (result is null)
            throw new InvalidOperationException("The IResult returned by the Delegate must not be null.");
        await result.ExecuteAsync(httpContext).ConfigureAwait(false);
    }
}
