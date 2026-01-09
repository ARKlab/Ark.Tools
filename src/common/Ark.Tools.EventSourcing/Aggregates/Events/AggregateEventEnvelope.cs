using Ark.Tools.EventSourcing.Events;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{

    public sealed class AggregateEventEnvelope<TAggregate>
        : EventEnvelope<IAggregateEvent<TAggregate>>
        where TAggregate : IAggregate
    {
        public AggregateEventEnvelope(IAggregateEvent<TAggregate> aggregateEvent, IMetadata metadata)
            : base(aggregateEvent, metadata)
        {
        }
=======
namespace Ark.Tools.EventSourcing.Aggregates;


public sealed class AggregateEventEnvelope<TAggregate>
    : EventEnvelope<IAggregateEvent<TAggregate>>
    where TAggregate : IAggregate
{
    public AggregateEventEnvelope(IAggregateEvent<TAggregate> aggregateEvent, IMetadata metadata)
        : base(aggregateEvent, metadata)
    {
>>>>>>> After


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