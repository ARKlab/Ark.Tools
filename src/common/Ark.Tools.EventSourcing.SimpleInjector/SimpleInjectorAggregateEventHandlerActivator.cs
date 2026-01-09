using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Events;

using SimpleInjector;

using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing.SimpleInjector(net10.0)', Before:
namespace Ark.Tools.EventSourcing.SimpleInjector
{
    public class SimpleInjectorAggregateEventHandlerActivator : IAggregateEventHandlerActivator
    {
        private readonly Container _container;

        public SimpleInjectorAggregateEventHandlerActivator(Container container)
        {
            _container = container;
        }

        public IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
            where TAggregate : IAggregate
            where TEvent : IAggregateEvent<TAggregate>
        {
            IServiceProvider provider = _container;
            var instance = (IAggregateEventHandler<TAggregate, TEvent>?)provider.GetService(typeof(IAggregateEventHandler<TAggregate, TEvent>));
            return instance;
        }
=======
namespace Ark.Tools.EventSourcing.SimpleInjector;

public class SimpleInjectorAggregateEventHandlerActivator : IAggregateEventHandlerActivator
{
    private readonly Container _container;

    public SimpleInjectorAggregateEventHandlerActivator(Container container)
    {
        _container = container;
    }

    public IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
        where TAggregate : IAggregate
        where TEvent : IAggregateEvent<TAggregate>
    {
        IServiceProvider provider = _container;
        var instance = (IAggregateEventHandler<TAggregate, TEvent>?)provider.GetService(typeof(IAggregateEventHandler<TAggregate, TEvent>));
        return instance;
>>>>>>> After


namespace Ark.Tools.EventSourcing.SimpleInjector;

    public class SimpleInjectorAggregateEventHandlerActivator : IAggregateEventHandlerActivator
    {
        private readonly Container _container;

        public SimpleInjectorAggregateEventHandlerActivator(Container container)
        {
            _container = container;
        }

        public IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
            where TAggregate : IAggregate
            where TEvent : IAggregateEvent<TAggregate>
        {
            IServiceProvider provider = _container;
            var instance = (IAggregateEventHandler<TAggregate, TEvent>?)provider.GetService(typeof(IAggregateEventHandler<TAggregate, TEvent>));
            return instance;
        }
    }