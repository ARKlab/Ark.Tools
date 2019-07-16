using Ark.Tools.EventSourcing.Events;

namespace Ark.Tools.EventSourcing.Aggregates
{
    public interface IAggregateEvent : IEvent
    {
    }

    public interface IAggregateEvent<TAggregate> : IAggregateEvent
        where TAggregate : IAggregateRoot
    {
    }
}
