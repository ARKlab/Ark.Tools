
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
    public interface IEventEnvelope<out T>
        where T : class, IEvent
    {
        T Event { get; }
        IMetadata Metadata { get; }
    }
=======
namespace Ark.Tools.EventSourcing.Events;

public interface IEventEnvelope<out T>
    where T : class, IEvent
{
    T Event { get; }
    IMetadata Metadata { get; }
>>>>>>> After
    namespace Ark.Tools.EventSourcing.Events;

    public interface IEventEnvelope<out T>
        where T : class, IEvent
    {
        T Event { get; }
        IMetadata Metadata { get; }
    }