﻿using Ark.Tools.EventSourcing.Aggregates;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.Store
{
    public interface IAggregateTransactionFactory<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        Task<IAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>> StartTransactionAsync(string id, CancellationToken ctk = default);
        Task<TAggregateState> LoadCapturedState(string id, CancellationToken ctk = default);
    }
}
