using Ark.Tools.EventSourcing.Aggregates;
using Ark.Tools.EventSourcing.Events;

using System;
using System.Collections.Generic;
using System.Linq;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Store
{
    public interface IAggregateEventStore
    {
        string? Id { get; }
        long AggregateVersion { get; }
        string? EventName { get; }
        string? TypeName { get; }

        Dictionary<string, string> Metadata { get; }
        void SetEvent(object @event);
    }


    public interface IAggregateEventStore<TAggregate, TEvent> : IAggregateEventStore
        where TEvent : class, IAggregateEvent<TAggregate>
        where TAggregate : class, IAggregate
    {
        TEvent? Event { get; }
    }

    public abstract class AggregateEventStore : IAggregateEventStore//, IAuditableEntity
    {
        public string? Id { get; set; }
        public string? TypeName { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
        public string? AggregateId { get; set; }
        public string? AggregateName { get; set; }
        public long AggregateVersion { get; set; }
        public string? EventName { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public abstract void SetEvent(object @event);
        public abstract object? GetEvent();
    }

    public sealed class AggregateEventStore<TAggregate, TEvent>
        : AggregateEventStore
        , IAggregateEventStore<TAggregate, TEvent>
        where TEvent : class, IAggregateEvent<TAggregate>
        where TAggregate : class, IAggregate
    {
#pragma warning disable CA1721 // Property names should not match get methods
        public TEvent? Event { get; set; }
#pragma warning restore CA1721 // Property names should not match get methods

        public override void SetEvent(object @event)
            => Event = @event as TEvent;

        public override object? GetEvent()
            => Event;
    }

    public static partial class Ex
    {
        public static AggregateEventStore ToStore<TAggregate>(this AggregateEventEnvelope<TAggregate> e)
            where TAggregate : IAggregate
        {
            var eventType = e.Event.GetType();
            var outboxType = typeof(AggregateEventStore<,>).MakeGenericType(typeof(TAggregate), eventType);

            var evt = (AggregateEventStore?)Activator.CreateInstance(outboxType);
            if (evt is null) throw new InvalidOperationException($"Failed to create instance of {outboxType}");

            evt.Id = e.Metadata.EventId;
            evt.EventName = e.Metadata.EventName;
            evt.AggregateId = e.Metadata.AggregateId;
            evt.AggregateName = e.Metadata.AggregateName;
            evt.AggregateVersion = e.Metadata.AggregateVersion;

            evt.Timestamp = e.Metadata.Timestamp;

            evt.Metadata = e.Metadata.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            evt.SetEvent(e.Event);

            return evt;
        }

        public static AggregateEventEnvelope<TAggregate> FromStore<TAggregate>(this AggregateEventStore store)
            where TAggregate : IAggregate
        {
            return new AggregateEventEnvelope<TAggregate>(
                store.GetEvent() as IAggregateEvent<TAggregate> ?? throw new InvalidOperationException("Stored Event cannot be null")
                , new Metadata(new MetadataContainer(store.Metadata)));

        }
=======
namespace Ark.Tools.EventSourcing.Store;

public interface IAggregateEventStore
{
    string? Id { get; }
    long AggregateVersion { get; }
    string? EventName { get; }
    string? TypeName { get; }

    Dictionary<string, string> Metadata { get; }
    void SetEvent(object @event);
}


public interface IAggregateEventStore<TAggregate, TEvent> : IAggregateEventStore
    where TEvent : class, IAggregateEvent<TAggregate>
    where TAggregate : class, IAggregate
{
    TEvent? Event { get; }
}

public abstract class AggregateEventStore : IAggregateEventStore//, IAuditableEntity
{
    public string? Id { get; set; }
    public string? TypeName { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
    public string? AggregateId { get; set; }
    public string? AggregateName { get; set; }
    public long AggregateVersion { get; set; }
    public string? EventName { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public abstract void SetEvent(object @event);
    public abstract object? GetEvent();
}

public sealed class AggregateEventStore<TAggregate, TEvent>
    : AggregateEventStore
    , IAggregateEventStore<TAggregate, TEvent>
    where TEvent : class, IAggregateEvent<TAggregate>
    where TAggregate : class, IAggregate
{
#pragma warning disable CA1721 // Property names should not match get methods
    public TEvent? Event { get; set; }
#pragma warning restore CA1721 // Property names should not match get methods

    public override void SetEvent(object @event)
        => Event = @event as TEvent;

    public override object? GetEvent()
        => Event;
}

public static partial class Ex
{
    public static AggregateEventStore ToStore<TAggregate>(this AggregateEventEnvelope<TAggregate> e)
        where TAggregate : IAggregate
    {
        var eventType = e.Event.GetType();
        var outboxType = typeof(AggregateEventStore<,>).MakeGenericType(typeof(TAggregate), eventType);

        var evt = (AggregateEventStore?)Activator.CreateInstance(outboxType);
        if (evt is null) throw new InvalidOperationException($"Failed to create instance of {outboxType}");

        evt.Id = e.Metadata.EventId;
        evt.EventName = e.Metadata.EventName;
        evt.AggregateId = e.Metadata.AggregateId;
        evt.AggregateName = e.Metadata.AggregateName;
        evt.AggregateVersion = e.Metadata.AggregateVersion;

        evt.Timestamp = e.Metadata.Timestamp;

        evt.Metadata = e.Metadata.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        evt.SetEvent(e.Event);

        return evt;
    }

    public static AggregateEventEnvelope<TAggregate> FromStore<TAggregate>(this AggregateEventStore store)
        where TAggregate : IAggregate
    {
        return new AggregateEventEnvelope<TAggregate>(
            store.GetEvent() as IAggregateEvent<TAggregate> ?? throw new InvalidOperationException("Stored Event cannot be null")
            , new Metadata(new MetadataContainer(store.Metadata)));
>>>>>>> After


namespace Ark.Tools.EventSourcing.Store;

    public interface IAggregateEventStore
    {
        string? Id { get; }
        long AggregateVersion { get; }
        string? EventName { get; }
        string? TypeName { get; }

        Dictionary<string, string> Metadata { get; }
        void SetEvent(object @event);
    }


    public interface IAggregateEventStore<TAggregate, TEvent> : IAggregateEventStore
        where TEvent : class, IAggregateEvent<TAggregate>
        where TAggregate : class, IAggregate
    {
        TEvent? Event { get; }
    }

    public abstract class AggregateEventStore : IAggregateEventStore//, IAuditableEntity
    {
        public string? Id { get; set; }
        public string? TypeName { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);
        public string? AggregateId { get; set; }
        public string? AggregateName { get; set; }
        public long AggregateVersion { get; set; }
        public string? EventName { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public abstract void SetEvent(object @event);
        public abstract object? GetEvent();
    }

    public sealed class AggregateEventStore<TAggregate, TEvent>
        : AggregateEventStore
        , IAggregateEventStore<TAggregate, TEvent>
        where TEvent : class, IAggregateEvent<TAggregate>
        where TAggregate : class, IAggregate
    {
#pragma warning disable CA1721 // Property names should not match get methods
        public TEvent? Event { get; set; }
#pragma warning restore CA1721 // Property names should not match get methods

        public override void SetEvent(object @event)
            => Event = @event as TEvent;

        public override object? GetEvent()
            => Event;
    }

    public static partial class Ex
    {
        public static AggregateEventStore ToStore<TAggregate>(this AggregateEventEnvelope<TAggregate> e)
            where TAggregate : IAggregate
        {
            var eventType = e.Event.GetType();
            var outboxType = typeof(AggregateEventStore<,>).MakeGenericType(typeof(TAggregate), eventType);

            var evt = (AggregateEventStore?)Activator.CreateInstance(outboxType);
            if (evt is null) throw new InvalidOperationException($"Failed to create instance of {outboxType}");

            evt.Id = e.Metadata.EventId;
            evt.EventName = e.Metadata.EventName;
            evt.AggregateId = e.Metadata.AggregateId;
            evt.AggregateName = e.Metadata.AggregateName;
            evt.AggregateVersion = e.Metadata.AggregateVersion;

            evt.Timestamp = e.Metadata.Timestamp;

            evt.Metadata = e.Metadata.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

            evt.SetEvent(e.Event);

            return evt;
        }

        public static AggregateEventEnvelope<TAggregate> FromStore<TAggregate>(this AggregateEventStore store)
            where TAggregate : IAggregate
        {
            return new AggregateEventEnvelope<TAggregate>(
                store.GetEvent() as IAggregateEvent<TAggregate> ?? throw new InvalidOperationException("Stored Event cannot be null")
                , new Metadata(new MetadataContainer(store.Metadata)));

        }
    }