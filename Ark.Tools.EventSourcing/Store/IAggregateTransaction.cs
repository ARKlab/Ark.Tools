using Ark.Tools.EventSourcing.Aggregates;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.Store
{
    public interface IAggregateTransaction<TAggregate, TAggregateState> : IDisposable
        where TAggregate : AggregateRoot<TAggregate, TAggregateState>, new()
        where TAggregateState : AggregateState<TAggregate, TAggregateState>, new()
    {
        TAggregate Aggregate { get; }

        Task SaveChangesAsync(CancellationToken ctk = default);
    }
}
