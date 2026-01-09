using Ark.Tools.EventSourcing.Aggregates;

using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Suffix is appropriate here")]
    public interface IAggregateEventHandler<TAggregate, TEvent>
        where TAggregate : IAggregate
        where TEvent : IAggregateEvent<TAggregate>
    {
        Task HandleAsync(TEvent @event, IMetadata metadata, CancellationToken ctk = default);
    }


=======
namespace Ark.Tools.EventSourcing.Events;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Suffix is appropriate here")]
public interface IAggregateEventHandler<TAggregate, TEvent>
    where TAggregate : IAggregate
    where TEvent : IAggregateEvent<TAggregate>
{
    Task HandleAsync(TEvent @event, IMetadata metadata, CancellationToken ctk = default);
>>>>>>> After
    namespace Ark.Tools.EventSourcing.Events;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Suffix is appropriate here")]
    public interface IAggregateEventHandler<TAggregate, TEvent>
        where TAggregate : IAggregate
        where TEvent : IAggregateEvent<TAggregate>
    {
        Task HandleAsync(TEvent @event, IMetadata metadata, CancellationToken ctk = default);
    }