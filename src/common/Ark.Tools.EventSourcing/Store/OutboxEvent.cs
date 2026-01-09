using Ark.Tools.EventSourcing.Events;


namespace Ark.Tools.EventSourcing.Store;

public abstract class OutboxEvent : IOutboxEvent
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new(System.StringComparer.Ordinal);

    public abstract void SetEvent(object @event);
    public abstract object GetEvent();
}

public sealed class OutboxEvent<TEvent> : OutboxEvent, IOutboxEvent<TEvent>
    where TEvent : class, IDomainEvent
{
#pragma warning disable CA1721 // Property names should not match get methods
    public TEvent? Event { get; set; }
#pragma warning restore CA1721 // Property names should not match get methods

    public override void SetEvent(object @event)
        => Event = @event as TEvent;

    public override object GetEvent()
        => Event!;
}