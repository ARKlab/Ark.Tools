// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using System.Reflection;

namespace Ark.Tools.Solid;

/// <summary>
/// Dispatches queries to the <see cref="IQueryHandler{TQuery, TResult}"/> registered in Microsoft dependency injection.
/// </summary>
/// <remarks>
/// The typed overload resolves the handler without reflection, so it works in trimmed and Native AOT applications; the
/// generated endpoints, gRPC services and messaging dispatch call it. Register the handlers as closed services, already
/// decorated: Microsoft dependency injection has no open-generic decorators.
/// </remarks>
/// <param name="services">The scoped service provider.</param>
public sealed class ServiceProviderQueryProcessor(IServiceProvider services) : IQueryProcessor
{
    /// <inheritdoc />
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public TResult Execute<TResult>(IQuery<TResult> query)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    [RequiresDynamicCode("Closes the handler type at run time with MakeGenericType.")]
    public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(query.GetType(), typeof(TResult));
        return await ServiceProviderProcessorInvoker.InvokeAsync<TResult>(services, handlerType, query, ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TQuery, TResult>(IQuery<TQuery, TResult> query, CancellationToken ctk = default)
        where TQuery : class, IQuery<TQuery, TResult>
    {
        if (query is not TQuery typedQuery)
            throw new ArgumentException($"Query of type '{query?.GetType()}' must be a '{typeof(TQuery)}'.", nameof(query));

        var handler = services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.ExecuteAsync(typedQuery, ctk).ConfigureAwait(false);
    }
}

/// <summary>
/// Dispatches requests to the <see cref="IRequestHandler{TRequest, TResponse}"/> registered in Microsoft dependency
/// injection.
/// </summary>
/// <remarks>
/// The typed overload resolves the handler without reflection, so it works in trimmed and Native AOT applications.
/// </remarks>
/// <param name="services">The scoped service provider.</param>
public sealed class ServiceProviderRequestProcessor(IServiceProvider services) : IRequestProcessor
{
    /// <inheritdoc />
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public TResponse Execute<TResponse>(IRequest<TResponse> request)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    [RequiresDynamicCode("Closes the handler type at run time with MakeGenericType.")]
    public async Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        return await ServiceProviderProcessorInvoker.InvokeAsync<TResponse>(services, handlerType, request, ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request, CancellationToken ctk = default)
        where TRequest : class, IRequest<TRequest, TResponse>
    {
        if (request is not TRequest typedRequest)
            throw new ArgumentException($"Request of type '{request?.GetType()}' must be a '{typeof(TRequest)}'.", nameof(request));

        var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return await handler.ExecuteAsync(typedRequest, ctk).ConfigureAwait(false);
    }
}

/// <summary>
/// Dispatches commands to the <see cref="ICommandHandler{TCommand}"/> registered in Microsoft dependency injection.
/// </summary>
/// <remarks>
/// The typed overload resolves the handler without reflection, so it works in trimmed and Native AOT applications.
/// </remarks>
/// <param name="services">The scoped service provider.</param>
public sealed class ServiceProviderCommandProcessor(IServiceProvider services) : ICommandProcessor
{
    /// <inheritdoc />
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public void Execute(ICommand command)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    [RequiresDynamicCode("Closes the handler type at run time with MakeGenericType.")]
    public async Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var handlerType = typeof(ICommandHandler<>).MakeGenericType(command.GetType());
        var handler = services.GetRequiredService(handlerType);
        var task = (Task)handlerType.GetMethod(nameof(ICommandHandler<ICommand>.ExecuteAsync))!.Invoke(handler, BindingFlags.DoNotWrapExceptions, null, [command, ctk], null)!;
        await task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
        where TCommand : class, ICommand<TCommand>
    {
        if (command is not TCommand typedCommand)
            throw new ArgumentException($"Command of type '{command?.GetType()}' must be a '{typeof(TCommand)}'.", nameof(command));

        var handler = services.GetRequiredService<ICommandHandler<TCommand>>();
        await handler.ExecuteAsync(typedCommand, ctk).ConfigureAwait(false);
    }
}

/// <summary>Registers the Microsoft dependency injection processors.</summary>
public static class ServiceProviderProcessorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ServiceProviderQueryProcessor"/>, <see cref="ServiceProviderRequestProcessor"/> and
    /// <see cref="ServiceProviderCommandProcessor"/> as scoped processors, unless processors are already registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkSolidServiceProviderProcessors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IQueryProcessor, ServiceProviderQueryProcessor>();
        services.TryAddScoped<IRequestProcessor, ServiceProviderRequestProcessor>();
        services.TryAddScoped<ICommandProcessor, ServiceProviderCommandProcessor>();
        return services;
    }
}

internal static class ServiceProviderProcessorInvoker
{
    [RequiresUnreferencedCode("Invokes the handler's ExecuteAsync through reflection.")]
    public static async Task<TResult> InvokeAsync<TResult>(
        IServiceProvider services, Type handlerType, object message, CancellationToken ctk)
    {
        var handler = services.GetRequiredService(handlerType);
        var task = (Task<TResult>)handlerType.GetMethod("ExecuteAsync")!.Invoke(handler, BindingFlags.DoNotWrapExceptions, null, [message, ctk], null)!;
        return await task.ConfigureAwait(false);
    }
}
