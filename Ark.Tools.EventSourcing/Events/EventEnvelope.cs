using EnsureThat;

namespace Ark.Tools.EventSourcing.Events
{
    public abstract class EventEnvelope<T> : IEventEnvelope<T>
        where T : class, IEvent
    {
        public T Event { get; }
        public IMetadata Metadata { get; }

        public EventEnvelope(T @event, IMetadata metadata)
        {
            Ensure.Any.IsNotNull(@event);
            Event = @event;
            Metadata = metadata;
        }
    }
}
