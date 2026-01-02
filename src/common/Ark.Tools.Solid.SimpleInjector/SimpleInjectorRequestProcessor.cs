// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using SimpleInjector;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.SimpleInjector
{
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
        public TResponse Execute<TResponse>(IRequest<TResponse> request)
        {
            dynamic requestHandler = _getHandlerInstance(request);

            return requestHandler.Execute((dynamic)request);
        }

        [DebuggerStepThrough]
        public async Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default)
        {
            dynamic requestHandler = _getHandlerInstance(request);
            return await requestHandler.ExecuteAsync((dynamic)request, ctk);
        }
    }
}