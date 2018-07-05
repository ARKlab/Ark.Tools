using SimpleInjector;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace Ark.Tools.Solid.SimpleInjector
{
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
        public TResult Execute<TResult>(IQuery<TResult> query)
        {
            dynamic queryHandler = _getHandlerInstance(query);
            
            return queryHandler.Execute((dynamic)query);
        }

        [DebuggerStepThrough]
        public async Task<TResult> ExecuteAsync<TResult>(IQuery<TResult> query, CancellationToken ctk = default(CancellationToken))
        {
            dynamic queryHandler = _getHandlerInstance(query);
            TResult res = await queryHandler.ExecuteAsync((dynamic)query, ctk).ConfigureAwait(false);
            return res;
        }
    }
}
