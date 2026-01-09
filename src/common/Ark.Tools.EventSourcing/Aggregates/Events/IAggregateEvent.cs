using Ark.Tools.EventSourcing.Events;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{
    public interface IAggregateEvent : IEvent
    {
    }

    public interface IAggregateEvent<TAggregate> : IAggregateEvent
        where TAggregate : IAggregate
    {
    }


=======
namespace Ark.Tools.EventSourcing.Aggregates;

public interface IAggregateEvent : IEvent
{
}

public interface IAggregateEvent<TAggregate> : IAggregateEvent
    where TAggregate : IAggregate
{
>>>>>>> After
    namespace Ark.Tools.EventSourcing.Aggregates;

    public interface IAggregateEvent : IEvent
    {
    }

    public interface IAggregateEvent<TAggregate> : IAggregateEvent
        where TAggregate : IAggregate
    {
    }