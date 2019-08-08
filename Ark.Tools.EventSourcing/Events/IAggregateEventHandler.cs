using Ark.Tools.EventSourcing.Aggregates;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.EventSourcing.Events
{
    public interface IAggregateEventHandler<TAggregate, TEvent>
        where TAggregate : IAggregate
        where TEvent : IAggregateEvent<TAggregate>
    {
        Task HandleAsync(TEvent @event, IMetadata metadata, CancellationToken ctk = default);
    }
}
