using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ark.Tools.EventSourcing.Store
{
	public interface IAggregateEventStore
    {
        string Id { get; }
        long AggregateVersion { get; }
        string EventName { get; }
        string TypeName { get; }
        
        Dictionary<string, string> Metadata { get; }
        void SetEvent(object @event);
    }


    public interface IAggregateEventStore<TAggregate, TEvent> : IAggregateEventStore
        where TEvent : class, IAggregateEvent<TAggregate>
        where TAggregate : class, IAggregateRoot
    {
        TEvent Event { get; }
    }

	public abstract class AggregateEventStore : IAggregateEventStore//, IAuditableEntity
	{
        public string Id { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, string> Metadata { get; set; }
        public string AggregateId { get; set; }
        public string AggregateName { get; set; }
        public long AggregateVersion { get; set; }
        public string EventName { get; set; }
        public DateTimeOffset Timestamp { get; set; }

		//public Guid AuditId { get; set; }

		public abstract void SetEvent(object @event);
        public abstract object GetEvent();
    }

    public sealed class AggregateEventStore<TAggregate, TEvent> 
        : AggregateEventStore
        , IAggregateEventStore<TAggregate, TEvent>
        where TEvent : class, IAggregateEvent<TAggregate>
        where TAggregate : class, IAggregateRoot
    {
        public TEvent Event { get; set; }

        public override void SetEvent(object @event)
            => Event = @event as TEvent;

        public override object GetEvent()
            => Event;
    }

    public static partial class Ex
    {
        public static AggregateEventStore ToStore<TAggregate>(this AggregateEventEnvelope<TAggregate> e)
            where TAggregate : IAggregateRoot
        {
            var eventType = e.Event.GetType();
            var outboxType = typeof(AggregateEventStore<,>).MakeGenericType(typeof(TAggregate), eventType);

            var evt = (AggregateEventStore)Activator.CreateInstance(outboxType);

            evt.Id = e.Metadata.EventId;
            evt.EventName = e.Metadata.EventName;
            evt.AggregateId = e.Metadata.AggregateId;
            evt.AggregateName = e.Metadata.AggregateName;
            evt.AggregateVersion = e.Metadata.AggregateVersion.Value;

            evt.Timestamp = e.Metadata.Timestamp.Value;

            evt.Metadata = e.Metadata.Values.ToDictionary(x => x.Key, x => x.Value);

            evt.SetEvent(e.Event);

            return evt;
        }

        public static AggregateEventEnvelope<TAggregate> FromStore<TAggregate>(this AggregateEventStore store)
            where TAggregate : IAggregateRoot
        {
            return new AggregateEventEnvelope<TAggregate>(store.GetEvent() as IAggregateEvent<TAggregate>, new Metadata(new MetadataContainer(store.Metadata)));
            
        }
    }
}
