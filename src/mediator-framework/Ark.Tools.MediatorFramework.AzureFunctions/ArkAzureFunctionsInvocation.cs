// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.AzureFunctions;

/// <summary>Provides the typed invocation boundary used by generated Functions.</summary>
public static class ArkAzureFunctionsInvocation
{
    /// <summary>
    /// Invokes the generated mediator pipeline for a request.
    /// </summary>
    /// <typeparam name="TRequest">The generated contract request type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the mediator pipeline.</returns>
    public static async Task<IResult> InvokeRequestAsync<TRequest, TResponse>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = await BindAsync<TRequest>(request, cancellationToken).ConfigureAwait(false);
        if (!binding.Succeeded)
            return Results.BadRequest();

        using var scope = BeginScope(request);
        var handler = scope.Container.GetInstance<IRequestHandler<TRequest, TResponse>>();
        var result = await handler.ExecuteAsync(binding.Value!, cancellationToken).ConfigureAwait(false);
        return result is null ? Results.NoContent() : Results.Ok(result);
    }

    /// <summary>Invokes a generated query through the application container.</summary>
    /// <typeparam name="TQuery">The generated query type.</typeparam>
    /// <typeparam name="TResponse">The query response type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the query handler.</returns>
    public static async Task<IResult> InvokeQueryAsync<TQuery, TResponse>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where TQuery : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = await BindAsync<TQuery>(request, cancellationToken).ConfigureAwait(false);
        if (!binding.Succeeded)
            return Results.BadRequest();

        using var scope = BeginScope(request);
        var handler = scope.Container.GetInstance<IQueryHandler<TQuery, TResponse>>();
        var result = await handler.ExecuteAsync(binding.Value!, cancellationToken).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    /// <summary>Invokes a generated command through the application container.</summary>
    /// <typeparam name="TCommand">The generated command type.</typeparam>
    /// <param name="request">The incoming ASP.NET Core request.</param>
    /// <param name="cancellationToken">The invocation cancellation token.</param>
    /// <returns>The HTTP result produced by the command handler.</returns>
    public static async Task<IResult> InvokeCommandAsync<TCommand>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(request);
        var binding = await BindAsync<TCommand>(request, cancellationToken).ConfigureAwait(false);
        if (!binding.Succeeded)
            return Results.BadRequest();

        using var scope = BeginScope(request);
        var handler = scope.Container.GetInstance<ICommandHandler<TCommand>>();
        await handler.ExecuteAsync(binding.Value!, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static ScopedLifestyle.Scope BeginScope(HttpRequest request)
    {
        var container = request.HttpContext.RequestServices.GetService<Container>()
            ?? throw new InvalidOperationException(
                "The Azure Functions mediator container is not registered. Call AddArkAzureFunctions with the application container.");
        return AsyncScopedLifestyle.BeginScope(container);
    }

    private static async Task<BindingResult<T>> BindAsync<T>(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        T? value;
        if (request.ContentLength is > 0 || request.Headers.ContentType.Any(value => value.Contains("json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                value = await request.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                }, cancellationToken).ConfigureAwait(false);
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
            catch (Exception) when (Activator.CreateInstance(typeof(T)) is null)
            {
                return BindingResult<T>.Failed;
            }
        }

        if (value is null)
            return BindingResult<T>.Failed;

        foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetCustomAttribute<ServerSetAttribute>() is not null)
            {
                if (property.CanWrite)
                    property.SetValue(value, property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null);
                continue;
            }

            var name = property.GetCustomAttribute<HttpRouteAttribute>()?.Name ?? property.Name;
            if (property.GetCustomAttribute<HttpRouteAttribute>() is not null && request.RouteValues.TryGetValue(name, out var route))
            {
                if (!TryConvert(route?.ToString(), property.PropertyType, out var converted))
                    return BindingResult<T>.Failed;
                property.SetValue(value, converted);
            }

            if (property.GetCustomAttribute<HttpQueryAttribute>() is not null
                && request.Query.TryGetValue(property.Name, out var query))
            {
                if (!TryConvert(query, property.PropertyType, out var converted))
                    return BindingResult<T>.Failed;
                property.SetValue(value, converted);
            }
        }

        return new BindingResult<T>(value, true);
    }

    private static bool TryConvert(string? input, Type type, out object? value)
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

            var converter = TypeDescriptor.GetConverter(target);
            value = converter.ConvertFromString(null, CultureInfo.InvariantCulture, input);
            return true;
        }
        catch (Exception) when (true)
        {
            value = null;
            return false;
        }
    }

    private readonly record struct BindingResult<T>(T? Value, bool Succeeded)
    {
        public static BindingResult<T> Failed => new(default, false);
    }
}
