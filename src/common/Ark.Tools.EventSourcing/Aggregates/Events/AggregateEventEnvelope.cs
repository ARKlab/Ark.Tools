using Ark.Tools.EventSourcing.Events;

namespace Ark.Tools.EventSourcing.Aggregates;


public sealed class AggregateEventEnvelope<TAggregate>
    : EventEnvelope<IAggregateEvent<TAggregate>>
    where TAggregate : IAggregate
{
    public AggregateEventEnvelope(IAggregateEvent<TAggregate> aggregateEvent, IMetadata metadata)
        : base(aggregateEvent, metadata)
    {
    }
}
