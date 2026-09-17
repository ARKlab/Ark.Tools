// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

internal sealed class ScopeAwareRequestProcessor(Container container, Func<IRequestProcessor> getInnerProcessor) : IRequestProcessor
{
    [DebuggerStepThrough]
#pragma warning disable CS0618 // Type or member is obsolete
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public TResponse Execute<TResponse>(IRequest<TResponse> request)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }
#pragma warning restore CS0618 // Type or member is obsolete

    [DebuggerStepThrough]
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    public async Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default)
    {
        return await ScopedProcessorExecution.ExecuteAsync(
            container,
            async () => await getInnerProcessor().ExecuteAsync(request, ctk).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(IRequest<TRequest, TResponse> request, CancellationToken ctk = default)
        where TRequest : class, IRequest<TRequest, TResponse>
    {
        return await ScopedProcessorExecution.ExecuteAsync(
            container,
            async () => await getInnerProcessor().ExecuteAsync<TRequest, TResponse>(request, ctk).ConfigureAwait(false)).ConfigureAwait(false);
    }
}

internal sealed class ScopeAwareQueryProcessor(Container container, Func<IQueryProcessor> getInnerProcessor) : IQueryProcessor
{
    [DebuggerStepThrough]
#pragma warning disable CS0618 // Type or member is obsolete
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public TResult Execute<TResult>(IQuery<TResult> query)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }
#pragma warning restore CS0618 // Type or member is obsolete

    [DebuggerStepThrough]
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default)
    {
        return await ScopedProcessorExecution.ExecuteAsync(
            container,
            async () => await getInnerProcessor().ExecuteAsync(query, ctk).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public async Task<TResult> ExecuteAsync<TQuery, TResult>(IQuery<TQuery, TResult> query, CancellationToken ctk = default)
        where TQuery : class, IQuery<TQuery, TResult>
    {
        return await ScopedProcessorExecution.ExecuteAsync(
            container,
            async () => await getInnerProcessor().ExecuteAsync<TQuery, TResult>(query, ctk).ConfigureAwait(false)).ConfigureAwait(false);
    }
}

internal sealed class ScopeAwareCommandProcessor(Container container, Func<ICommandProcessor> getInnerProcessor) : ICommandProcessor
{
    [DebuggerStepThrough]
#pragma warning disable CS0618 // Type or member is obsolete
    [Obsolete("Use ExecuteAsync instead. Synchronous execution will be removed in a future version.", error: true)]
    public void Execute(ICommand command)
    {
        throw new NotSupportedException("Synchronous execution is not supported. Use ExecuteAsync instead.");
    }
#pragma warning restore CS0618 // Type or member is obsolete

    [DebuggerStepThrough]
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    public async Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
    {
        await ScopedProcessorExecution.ExecuteAsync(
            container,
            async () => await getInnerProcessor().ExecuteAsync(command, ctk).ConfigureAwait(false)).ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public async Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
        where TCommand : class, ICommand<TCommand>
    {
        await ScopedProcessorExecution.ExecuteAsync(
            container,
            async () => await getInnerProcessor().ExecuteAsync<TCommand>(command, ctk).ConfigureAwait(false)).ConfigureAwait(false);
    }
}
