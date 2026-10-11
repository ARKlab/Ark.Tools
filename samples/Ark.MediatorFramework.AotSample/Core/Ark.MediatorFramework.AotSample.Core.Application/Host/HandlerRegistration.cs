// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Solid.Decorators;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.MediatorFramework.AotSample.Core.Application.Host;

/// <summary>Registers handlers wrapped in the Ark.Tools.Solid decorators, without reflection.</summary>
/// <remarks>
/// Microsoft dependency injection has no open-generic decorators that work under Native AOT. A generic method is the
/// AOT-friendly form: written once, it applies the same chain to every handler, and neither the handlers nor the
/// decorators know the composition. See <c>docs/design/mediator-framework/aot-di-comparison.md</c>.
/// </remarks>
public static class HandlerRegistration
{
    /// <summary>Registers a request handler.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddRequest<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IRequestHandler<TRequest, TResponse>>(static sp =>
            new ExceptionLogRequestDecorator<TRequest, TResponse>(
                new ProfileRequestDecorator<TRequest, TResponse>(sp.GetRequiredService<THandler>())));
        return services;
    }

    /// <summary>Registers a query handler.</summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddQuery<TQuery, TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IQueryHandler<TQuery, TResult>>(static sp =>
            new ExceptionLogQueryDecorator<TQuery, TResult>(
                new ProfileQueryDecorator<TQuery, TResult>(sp.GetRequiredService<THandler>())));
        return services;
    }

    /// <summary>Registers a command handler.</summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCommand<TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand>>(static sp =>
            new ExceptionLogCommandDecorator<TCommand>(
                new ProfileCommandDecorator<TCommand>(sp.GetRequiredService<THandler>())));
        return services;
    }
}
