// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

public class SimpleInjectorQueryProcessor : IQueryProcessor
{
    private readonly Container _container;

    public SimpleInjectorQueryProcessor(Container container)
    {
        _container = container;
    }

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
        return await QueryHandlerInvokerCache<TResult>.ExecuteAsync(_container, query, ctk).ConfigureAwait(false);
    }

    [DebuggerStepThrough]
    public async Task<TResult> ExecuteAsync<TQuery, TResult>(IQuery<TQuery, TResult> query, CancellationToken ctk = default)
        where TQuery : class, IQuery<TQuery, TResult>
    {
        if (query is not TQuery typedQuery)
            throw new ArgumentException($"Query of type '{query.GetType()}' must be a '{typeof(TQuery)}'.", nameof(query));

        var handler = _container.GetInstance<IQueryHandler<TQuery, TResult>>();
        return await handler.ExecuteAsync(typedQuery, ctk).ConfigureAwait(false);
    }
}