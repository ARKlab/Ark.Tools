// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

public class SimpleInjectorRequestProcessor : IRequestProcessor
{
    private readonly Container _container;
    private ISolidSimpleInjectorDispatcher? _dispatcher;

    public SimpleInjectorRequestProcessor(Container container)
    {
        ArgumentNullException.ThrowIfNull(container);
        _container = container;
        _dispatcher = SolidSimpleInjectorDispatchRegistry.Current;
    }

    /// <summary>
    /// Creates a request processor using generated dispatch when available.
    /// </summary>
    /// <param name="container">The verified SimpleInjector container.</param>
    /// <param name="dispatcher">The generated dispatcher.</param>
    /// <returns>A request processor using the generated dispatcher.</returns>
    public static SimpleInjectorRequestProcessor Create(Container container, ISolidSimpleInjectorDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return new SimpleInjectorRequestProcessor(container) { Dispatcher = dispatcher };
    }

    /// <summary>
    /// Gets or sets the optional generated dispatcher.
    /// </summary>
    /// <value>The generated dispatcher, or <see langword="null"/> to use the compatibility fallback.</value>
    public ISolidSimpleInjectorDispatcher? Dispatcher
    {
        get => _dispatcher;
        set => _dispatcher = value;
    }

    private object _getHandlerInstance<TResponse>(IRequest<TResponse> request)
    {
        var RequestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(RequestType, typeof(TResponse));

        return _container.GetInstance(handlerType);
    }

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
        ArgumentNullException.ThrowIfNull(request);
        if (_dispatcher?.TryExecuteRequest(_container, request, ctk, out var generatedExecution) == true)
        {
            return await generatedExecution!.ConfigureAwait(false);
        }

        dynamic requestHandler = _getHandlerInstance(request);
        return await requestHandler.ExecuteAsync((dynamic)request, ctk).ConfigureAwait(false);
    }
}