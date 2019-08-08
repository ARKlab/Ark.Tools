using System;
using Ark.Tools.EventSourcing.Events;
using Ark.Tools.EventSourcing.Aggregates;
using SimpleInjector;

namespace Ark.Tools.EventSourcing.SimpleInjector
{
	public class SimpleInjectorAggregateEventHandlerActivator : IAggregateEventHandlerActivator
	{
		private readonly Container _container;

		public SimpleInjectorAggregateEventHandlerActivator(Container container)
		{
			_container = container;
		}

		public IAggregateEventHandler<TAggregate, TEvent> GetHandler<TAggregate, TEvent>(TEvent @event)
			where TAggregate : IAggregate
			where TEvent : IAggregateEvent<TAggregate>
		{
			IServiceProvider provider = _container;
			var instance = (IAggregateEventHandler<TAggregate, TEvent>)provider.GetService(typeof(IAggregateEventHandler<TAggregate, TEvent>));
			return instance;
		}
	}
}
