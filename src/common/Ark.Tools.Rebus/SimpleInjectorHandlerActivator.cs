using Rebus.Activation;
using Rebus.Handlers;
using Rebus.Transport;

using SimpleInjector;


namespace Ark.Tools.Rebus;

public class SimpleInjectorHandlerActivator : IHandlerActivator
{
    readonly Container _container;
    public SimpleInjectorHandlerActivator(Container container) { _container = container; }

    public Task<IEnumerable<IHandleMessages<TMessage>>> GetHandlers<TMessage>(
        TMessage m, ITransactionContext transactionContext) =>
        Task.FromResult(_container.GetAllInstances<IHandleMessages<TMessage>>());
}