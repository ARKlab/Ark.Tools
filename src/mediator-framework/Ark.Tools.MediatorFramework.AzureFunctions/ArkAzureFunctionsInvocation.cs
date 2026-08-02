// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System.Reflection;
using System.Text.Json;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Provides the typed invocation boundary used by generated Functions.</summary>
public static class ArkAzureFunctionsInvocation
{
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
            return Results.BadRequest();

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
            return Results.BadRequest();

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
            return Results.BadRequest();

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
            catch (JsonException)
            {
                return BindingResult<T>.Failed;
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
                return BindingResult<T>.Failed;
            }
        }

        if (value is null)
            return BindingResult<T>.Failed;

        foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (HasAttribute(property, "Ark.MediatorFramework.ServerSetAttribute"))
            {
                if (property.CanWrite)
                    property.SetValue(value, null);
                continue;
            }

            var routeAttribute = GetAttribute(property, "Ark.MediatorFramework.HttpRouteAttribute");
            var name = routeAttribute?.ConstructorArguments.FirstOrDefault().Value as string ?? property.Name;
            if (request.RouteValues.TryGetValue(name, out var route))
            {
                if (!TryConvert(route?.ToString(), property.PropertyType, out var converted))
                    return BindingResult<T>.Failed;
                property.SetValue(value, converted);
            }

            if (HasAttribute(property, "Ark.MediatorFramework.HttpQueryAttribute")
                && request.Query.TryGetValue(property.Name, out var query))
            {
                if (!TryConvert(query, property.PropertyType, out var converted))
                    return BindingResult<T>.Failed;
                property.SetValue(value, converted);
            }
        }

        return new BindingResult<T>(value, true);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "HTTP scalar types are selected by generated contract metadata.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "HTTP scalar types are selected by generated contract metadata.")]
    private static bool TryConvert(
        string? input,
        Type type,
        out object? value)
    {
        if (input is null)
        {
            value = null;
            return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        }

        var target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (target == typeof(string))
            {
                value = input;
                return true;
            }

            var converter = System.ComponentModel.TypeDescriptor.GetConverter(target);
            value = converter.ConvertFromString(null, System.Globalization.CultureInfo.InvariantCulture, input);
            return true;
        }
        catch (FormatException)
        {
            value = null;
            return false;
        }
        catch (NotSupportedException)
        {
            value = null;
            return false;
        }
    }

    private static bool HasAttribute(PropertyInfo property, string metadataName)
    {
        return GetAttribute(property, metadataName) is not null;
    }

    private static CustomAttributeData? GetAttribute(PropertyInfo property, string metadataName)
    {
        return property.CustomAttributes.FirstOrDefault(attribute =>
            string.Equals(attribute.AttributeType.FullName, metadataName, StringComparison.Ordinal));
    }

    private readonly record struct BindingResult<T>(T? Value, bool Succeeded)
    {
        public static BindingResult<T> Failed => new(default, false);
    }
}
