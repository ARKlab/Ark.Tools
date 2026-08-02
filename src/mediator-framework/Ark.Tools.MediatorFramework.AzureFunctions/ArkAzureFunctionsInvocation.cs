// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System.Reflection;
using System.Text.Json;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Provides the typed invocation boundary used by generated Functions.</summary>
public static class ArkAzureFunctionsInvocation
{
    /// <summary>
    /// Authenticates a generated endpoint and returns a challenge result when authentication fails.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="allowAnonymous">Whether the endpoint permits anonymous access.</param>
    /// <returns>
    /// <see langword="null"/> when the request may continue; otherwise a result that challenges
    /// the caller.
    /// </returns>
    public static async Task<IResult?> AuthenticateAsync(HttpContext context, bool allowAnonymous)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (allowAnonymous)
        {
            context.User ??= new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity());
            return null;
        }

        var authentication = context.RequestServices.GetService<IAuthenticationService>()
            ?? throw new InvalidOperationException(
                "The Azure Functions authentication service is not registered. Configure ASP.NET Core authentication.");
        var options = context.RequestServices.GetService<IOptions<ArkAzureFunctionsAuthenticationOptions>>()?.Value;
        var scheme = options?.Scheme;
        var result = await authentication.AuthenticateAsync(context, scheme).ConfigureAwait(false);
        if (!result.Succeeded || result.Principal is null)
            return Results.Challenge(scheme is null ? null : [scheme]);

        context.User = result.Principal;
        return null;
    }

    /// <summary>
    /// Invokes the generated mediator pipeline for a request.
    /// </summary>
    /// <typeparam name="TRequest">The generated contract request type.</typeparam>
    /// <typeparam name="TResponse">The request response type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the mediator pipeline.</returns>
    public static async Task<IResult> InvokeRequestAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TRequest,
        TResponse>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = await BindAsync<TRequest>(request, cancellationToken).ConfigureAwait(false);
        if (!binding.Succeeded)
            return Results.Problem(statusCode: 400, title: "BINDING_FAILURE", detail: binding.Error);

        var (container, scope) = BeginScope(request);
        await using (scope.ConfigureAwait(false))
        {
            var handler = container.GetInstance<IRequestHandler<TRequest, TResponse>>();
            var result = await handler.ExecuteAsync(binding.Value!, cancellationToken).ConfigureAwait(false);
            return result is null ? Results.NoContent() : Results.Ok(result);
        }
    }

    /// <summary>Invokes a generated query through the application container.</summary>
    /// <typeparam name="TQuery">The generated query type.</typeparam>
    /// <typeparam name="TResponse">The query response type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the query handler.</returns>
    public static async Task<IResult> InvokeQueryAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TQuery,
        TResponse>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = await BindAsync<TQuery>(request, cancellationToken).ConfigureAwait(false);
        if (!binding.Succeeded)
            return Results.Problem(statusCode: 400, title: "BINDING_FAILURE", detail: binding.Error);

        var (container, scope) = BeginScope(request);
        await using (scope.ConfigureAwait(false))
        {
            var handler = container.GetInstance<IQueryHandler<TQuery, TResponse>>();
            var result = await handler.ExecuteAsync(binding.Value!, cancellationToken).ConfigureAwait(false);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
    }

    /// <summary>Invokes a generated command through the application container.</summary>
    /// <typeparam name="TCommand">The generated command type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the command handler.</returns>
    public static async Task<IResult> InvokeCommandAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] TCommand>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = await BindAsync<TCommand>(request, cancellationToken).ConfigureAwait(false);
        if (!binding.Succeeded)
            return Results.Problem(statusCode: 400, title: "BINDING_FAILURE", detail: binding.Error);

        var (container, scope) = BeginScope(request);
        await using (scope.ConfigureAwait(false))
        {
            var handler = container.GetInstance<ICommandHandler<TCommand>>();
            await handler.ExecuteAsync(binding.Value!, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
    }

    private static (Container Container, Scope Scope) BeginScope(HttpRequest request)
    {
        var container = request.HttpContext.RequestServices.GetService<Container>()
            ?? throw new InvalidOperationException(
                "The Azure Functions mediator container is not registered. Call AddArkAzureFunctions with the application container.");
        return (container, AsyncScopedLifestyle.BeginScope(container));
    }

    // ponytail: reflection on typeof(T) is performed once per T via the static generic cache PropertyCache<T>;
    // upgrade path is the source generator which emits per-property code with zero runtime reflection.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Generated contract types are preserved by the source generator.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Generated contract types are preserved by the source generator.")]
    private static async Task<BindingResult<T>> BindAsync<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        T? value;
        if (request.ContentLength is > 0 || request.Headers.ContentType.ToString().Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                value = await request.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                return BindingResult<T>.Fail("Request body could not be deserialized: " + ex.Message);
            }
        }
        else
        {
            try
            {
                value = Activator.CreateInstance<T>();
            }
            catch (MissingMethodException)
            {
                return BindingResult<T>.Fail("Contract type '" + typeof(T).Name + "' does not have a public parameterless constructor.");
            }
        }

        if (value is null)
            return BindingResult<T>.Fail("Request body deserialized to null.");

        foreach (var entry in PropertyCache<T>.Entries)
        {
            if (entry.IsServerSet)
            {
                // Only reset writable properties; skip non-nullable value types to avoid InvalidCastException.
                if (entry.Property.CanWrite && entry.IsNullableOrReference)
                    entry.Property.SetValue(value, null);
                else if (entry.Property.CanWrite)
                    entry.Property.SetValue(value, entry.DefaultValue);
                continue;
            }

            var bindingName = entry.BindingName;
            if (entry.IsRoute && request.RouteValues.TryGetValue(bindingName, out var route))
            {
                if (!TryConvertObject(route?.ToString(), entry.Property.PropertyType, out var converted, out var convertError))
                    return BindingResult<T>.Fail("Route value '" + bindingName + "' could not be bound: " + convertError);
                entry.Property.SetValue(value, converted);
            }

            if (entry.IsQuery && request.Query.TryGetValue(entry.Property.Name, out var query))
            {
                if (!TryConvertObject(query, entry.Property.PropertyType, out var converted, out var convertError))
                    return BindingResult<T>.Fail("Query value '" + entry.Property.Name + "' could not be bound: " + convertError);
                entry.Property.SetValue(value, converted);
            }
        }

        return new BindingResult<T>(value, true, null);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "HTTP scalar types are handled by cached TypeConverter lookups; generated code uses ArkTypeConverter.TryConvert<T> with known types.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "HTTP scalar types are handled by cached TypeConverter lookups; generated code uses ArkTypeConverter.TryConvert<T> with known types.")]
    private static bool TryConvertObject(
        string? input,
        Type type,
        out object? value,
        out string? error)
    {
        if (input is null)
        {
            value = null;
            error = null;
            if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
            {
                error = "null is not valid for non-nullable type '" + type.Name + "'.";
                return false;
            }
            return true;
        }

        var target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (target == typeof(string))
            {
                value = input;
                error = null;
                return true;
            }

            var converter = System.ComponentModel.TypeDescriptor.GetConverter(target);
            value = converter.ConvertFromString(null, System.Globalization.CultureInfo.InvariantCulture, input);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is FormatException
                                        or NotSupportedException
                                        or InvalidCastException
                                        or OverflowException
                                        or ArgumentException)
        {
            value = null;
            error = "'" + input + "' cannot be converted to " + target.Name + ": " + ex.Message;
            return false;
        }
    }

    // Static generic cache: typeof(T).GetProperties() runs exactly once per T.
    private static class PropertyCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>
    {
        public static readonly PropertyEntry[] Entries = Build();

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "PropertyCache<T> is only used for T types preserved by the source generator.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "PropertyInfo.PropertyType refers to types preserved by the source generator.")]
        private static PropertyEntry[] Build()
        {
            return typeof(T)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(p =>
                {
                    var routeAttr = p.CustomAttributes.FirstOrDefault(a =>
                        string.Equals(a.AttributeType.FullName, "Ark.MediatorFramework.HttpRouteAttribute", StringComparison.Ordinal));
                    var bindingName = routeAttr?.ConstructorArguments.FirstOrDefault().Value as string ?? p.Name;
                    var isRoute = routeAttr is not null;
                    var isQuery = p.CustomAttributes.Any(a =>
                        string.Equals(a.AttributeType.FullName, "Ark.MediatorFramework.HttpQueryAttribute", StringComparison.Ordinal));
                    var isServerSet = p.CustomAttributes.Any(a =>
                        string.Equals(a.AttributeType.FullName, "Ark.MediatorFramework.ServerSetAttribute", StringComparison.Ordinal));
                    var propType = p.PropertyType;
                    var isNullableOrRef = !propType.IsValueType || Nullable.GetUnderlyingType(propType) is not null;
                    var defaultValue = propType.IsValueType ? Activator.CreateInstance(propType) : null;
                    return new PropertyEntry(p, bindingName, isRoute, isQuery, isServerSet, isNullableOrRef, defaultValue);
                })
                .ToArray();
        }
    }

    private sealed class PropertyEntry(
        PropertyInfo property,
        string bindingName,
        bool isRoute,
        bool isQuery,
        bool isServerSet,
        bool isNullableOrReference,
        object? defaultValue)
    {
        public PropertyInfo Property { get; } = property;
        public string BindingName { get; } = bindingName;
        public bool IsRoute { get; } = isRoute;
        public bool IsQuery { get; } = isQuery;
        public bool IsServerSet { get; } = isServerSet;
        public bool IsNullableOrReference { get; } = isNullableOrReference;
        public object? DefaultValue { get; } = defaultValue;
    }

    private readonly record struct BindingResult<T>(T? Value, bool Succeeded, string? Error)
    {
        public static BindingResult<T> Fail(string error) => new(default, false, error);
    }
}
