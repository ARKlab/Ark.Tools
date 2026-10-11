// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Microsoft.Extensions.DependencyInjection;

namespace Ark.MediatorFramework.Sample.Aot.Application;

// shortcut: Ark.Tools ships processors only for SimpleInjector, which Native AOT cannot use; move these into
// Ark.Tools.Solid when the AOT host leaves the prototype stage. They support only the typed, reflection-free overloads.

/// <summary>Dispatches requests to handlers registered in the Microsoft service provider.</summary>
/// <param name="services">The scoped service provider.</param>
public sealed class ServiceProviderRequestProcessor(IServiceProvider services) : IRequestProcessor
{
    /// <inheritdoc />
    [Obsolete("Use ExecuteAsync instead.", error: true)]
    public TResponse Execute<TResponse>(IRequest<TResponse> request) => throw new NotSupportedException();

    /// <inheritdoc />
    [RequiresUnreferencedCode("Not supported: Native AOT dispatch uses the typed overload.")]
    public Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request, CancellationToken ctk = default)
        where TRequest : class, IRequest<TRequest, TResponse>
    {
        var handler = services.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return await handler.ExecuteAsync((TRequest)request, ctk).ConfigureAwait(false);
    }
}

/// <summary>Dispatches queries to handlers registered in the Microsoft service provider.</summary>
/// <param name="services">The scoped service provider.</param>
public sealed class ServiceProviderQueryProcessor(IServiceProvider services) : IQueryProcessor
{
    /// <inheritdoc />
    [Obsolete("Use ExecuteAsync instead.", error: true)]
    public TResult Execute<TResult>(IQuery<TResult> query) => throw new NotSupportedException();

    /// <inheritdoc />
    [RequiresUnreferencedCode("Not supported: Native AOT dispatch uses the typed overload.")]
    public Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public async Task<TResult> ExecuteAsync<TQuery, TResult>(IQuery<TQuery, TResult> query, CancellationToken ctk = default)
        where TQuery : class, IQuery<TQuery, TResult>
    {
        var handler = services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.ExecuteAsync((TQuery)query, ctk).ConfigureAwait(false);
    }
}

/// <summary>Dispatches commands to handlers registered in the Microsoft service provider.</summary>
/// <param name="services">The scoped service provider.</param>
public sealed class ServiceProviderCommandProcessor(IServiceProvider services) : ICommandProcessor
{
    /// <inheritdoc />
    [Obsolete("Use ExecuteAsync instead.", error: true)]
    public void Execute(ICommand command) => throw new NotSupportedException();

    /// <inheritdoc />
    [RequiresUnreferencedCode("Not supported: Native AOT dispatch uses the typed overload.")]
    public Task ExecuteAsync(ICommand command, CancellationToken ctk = default) => throw new NotSupportedException();

    /// <inheritdoc />
    public async Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
        where TCommand : class, ICommand<TCommand>
    {
        var handler = services.GetRequiredService<ICommandHandler<TCommand>>();
        await handler.ExecuteAsync((TCommand)command, ctk).ConfigureAwait(false);
    }
}
