using Ark.Tools.EventSourcing.Aggregates;

using SimpleInjector;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing.SimpleInjector(net10.0)', Before:
namespace Ark.Tools.EventSourcing.SimpleInjector
{
    public sealed class SimpleInjectorAggregateRootFactory : IAggregateRootFactory
    {
        private readonly Container _container;

        public SimpleInjectorAggregateRootFactory(Container container)
        {
            _container = container;
        }

        public TAggregateRoot Create<TAggregateRoot>()
            where TAggregateRoot : class, IAggregateRoot
        {
            return _container.GetInstance<TAggregateRoot>();
        }
=======
namespace Ark.Tools.EventSourcing.SimpleInjector;

public sealed class SimpleInjectorAggregateRootFactory : IAggregateRootFactory
{
    private readonly Container _container;

    public SimpleInjectorAggregateRootFactory(Container container)
    {
        _container = container;
    }

    public TAggregateRoot Create<TAggregateRoot>()
        where TAggregateRoot : class, IAggregateRoot
    {
        return _container.GetInstance<TAggregateRoot>();
>>>>>>> After


namespace Ark.Tools.EventSourcing.SimpleInjector;

    public sealed class SimpleInjectorAggregateRootFactory : IAggregateRootFactory
    {
        private readonly Container _container;

        public SimpleInjectorAggregateRootFactory(Container container)
        {
            _container = container;
        }

        public TAggregateRoot Create<TAggregateRoot>()
            where TAggregateRoot : class, IAggregateRoot
        {
            return _container.GetInstance<TAggregateRoot>();
        }
    }