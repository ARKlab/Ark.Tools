using Ark.Tools.EventSourcing.Aggregates;

namespace Ark.Tools.EventSourcing.Events;

public interface IAggregateEventHandlerActivator
{
    IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
        where TAggregate : IAggregate
        where TEvent : IAggregateEvent<TAggregate>
        ;
}