namespace Ark.Tools.EventSourcing.Events;

public interface IEventEnvelope<out T>
    where T : class, IEvent
{
    T Event { get; }
    IMetadata Metadata { get; }
}