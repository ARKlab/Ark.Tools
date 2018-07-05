using SimpleInjector;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace Ark.Tools.Solid.SimpleInjector
{
    public class SimpleInjectorCommandProcessor : ICommandProcessor
    {
        private readonly Container _container;

        public SimpleInjectorCommandProcessor(Container container)
        {
            _container = container;
        }

        private object _getHandlerInstance(object command)
        {
            var commandType = command.GetType();
            var handlerType = typeof(ICommandHandler<>).MakeGenericType(commandType);

            return _container.GetInstance(handlerType);
        }

        [DebuggerStepThrough]
        public void Execute(object command)
        {
            dynamic commandHandler = _getHandlerInstance(command);
            commandHandler.Execute((dynamic)command);
        }

        [DebuggerStepThrough]
        public async Task ExecuteAsync(object command, CancellationToken ctk = default(CancellationToken))
        {
            dynamic commandHandler = _getHandlerInstance(command);
            await commandHandler.ExecuteAsync((dynamic)command, ctk).ConfigureAwait(false);
        }


    }
}
