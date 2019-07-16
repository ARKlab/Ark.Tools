using Ark.Tools.EventSourcing.Events;
using System.Collections.Generic;

namespace Ark.Tools.EventSourcing.Store
{
	public abstract class OutboxEvent : IOutboxEvent
    {
        public string Id { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public abstract void SetEvent(object @event);
        public abstract object GetEvent();
    }

    public sealed class OutboxEvent<TEvent> : OutboxEvent, IOutboxEvent<TEvent>
        where TEvent : class, IDomainEvent
    {
        public TEvent Event { get; set; }

        public override void SetEvent(object @event)
            => Event = @event as TEvent;

        public override object GetEvent()
            => Event;
    }
}
