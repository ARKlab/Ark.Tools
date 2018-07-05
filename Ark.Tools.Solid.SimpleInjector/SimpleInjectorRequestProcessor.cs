using SimpleInjector;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

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
        public async Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default(CancellationToken))
        {
            dynamic requestHandler = _getHandlerInstance(request);
            return await requestHandler.ExecuteAsync((dynamic)request, ctk).ConfigureAwait(false);
        }
    }
}
