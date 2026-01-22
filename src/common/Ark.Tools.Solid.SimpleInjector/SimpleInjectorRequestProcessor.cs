// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Solid.SimpleInjector;

public class SimpleInjectorRequestProcessor : IRequestProcessor
{
    private readonly Container _container;

    public SimpleInjectorRequestProcessor(Container container)
    {
        _container = container;
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
        dynamic requestHandler = _getHandlerInstance(request);
        return await requestHandler.ExecuteAsync((dynamic)request, ctk);
    }
}