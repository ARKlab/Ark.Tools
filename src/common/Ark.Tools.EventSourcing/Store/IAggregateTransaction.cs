using Ark.Tools.EventSourcing.Aggregates;


namespace Ark.Tools.EventSourcing.Store;

public interface IAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate> : IDisposable
    where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
    where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
    where TAggregate : IAggregate
{
    TAggregateRoot Aggregate { get; }

    IEnumerable<AggregateEventEnvelope<TAggregate>> History { get; }

    Task SaveChangesAsync(CancellationToken ctk = default);
}