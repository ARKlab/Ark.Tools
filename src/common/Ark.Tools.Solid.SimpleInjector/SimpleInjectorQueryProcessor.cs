// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

public class SimpleInjectorQueryProcessor : IQueryProcessor
{
    private readonly Container _container;
    private readonly ISolidSimpleInjectorDispatcher? _dispatcher;

    public SimpleInjectorQueryProcessor(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
    }

    /// <summary>
    /// Creates a query processor using generated dispatch when available.
    /// </summary>
    /// <param name="container">The verified SimpleInjector container.</param>
    /// <param name="dispatcher">The generated dispatcher.</param>
    /// <returns>A query processor using the generated dispatcher.</returns>
    public static SimpleInjectorQueryProcessor Create(Container container, ISolidSimpleInjectorDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new SimpleInjectorQueryProcessor(container, dispatcher, privateConstruction: true);
    }

    private SimpleInjectorQueryProcessor(Container container, ISolidSimpleInjectorDispatcher dispatcher, bool privateConstruction)
        : this(container)
    {
        _dispatcher = dispatcher;
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
    [RequiresUnreferencedCode("Uses dynamic invocation for handler dispatch. Handler types must be preserved.")]
    public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_dispatcher?.TryExecuteQuery(_container, query, ctk, out var generatedExecution) == true)
        {
            return await generatedExecution!.ConfigureAwait(false);
        }

        dynamic queryHandler = _getHandlerInstance(query);
        TResult res = await queryHandler.ExecuteAsync((dynamic)query, ctk).ConfigureAwait(false);
        return res;
    }
}