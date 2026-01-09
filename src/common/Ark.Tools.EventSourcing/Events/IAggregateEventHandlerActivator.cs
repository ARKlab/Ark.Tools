using Ark.Tools.EventSourcing.Aggregates;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
    public interface IAggregateEventHandlerActivator
    {
        IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
            where TAggregate : IAggregate
            where TEvent : IAggregateEvent<TAggregate>
            ;
    }


=======
namespace Ark.Tools.EventSourcing.Events;

public interface IAggregateEventHandlerActivator
{
    IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
        where TAggregate : IAggregate
        where TEvent : IAggregateEvent<TAggregate>
        ;
>>>>>>> After
    namespace Ark.Tools.EventSourcing.Events;

    public interface IAggregateEventHandlerActivator
    {
        IAggregateEventHandler<TAggregate, TEvent>? GetHandler<TAggregate, TEvent>(TEvent @event)
            where TAggregate : IAggregate
            where TEvent : IAggregateEvent<TAggregate>
            ;
    }