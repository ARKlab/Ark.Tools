using Ark.Tools.EventSourcing.Aggregates;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.Store
{
    public interface IAggregateTransactionFactory<TAggregate, TAggregateState>
        where TAggregate : AggregateRoot<TAggregate, TAggregateState>, new()
        where TAggregateState : AggregateState<TAggregate, TAggregateState>, new()
    {
        Task<IAggregateTransaction<TAggregate, TAggregateState>> StartTransactionAsync(string id, CancellationToken ctk = default);
		Task<TAggregateState> LoadCapturedState(string id, CancellationToken ctk = default);
	}
}
