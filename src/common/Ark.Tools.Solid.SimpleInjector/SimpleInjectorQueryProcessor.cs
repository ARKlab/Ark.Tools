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

    private object _getHandlerInstance<TResult>(IQuery<TResult> query)
    {
        var queryType = query.GetType();
        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(queryType, typeof(TResult));

        return _container.GetInstance(handlerType);
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
    public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default)
    {
        dynamic queryHandler = _getHandlerInstance(query);
        TResult res = await queryHandler.ExecuteAsync((dynamic)query, ctk);
        return res;
    }
}