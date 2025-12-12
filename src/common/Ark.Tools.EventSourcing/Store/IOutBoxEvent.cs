using Ark.Tools.EventSourcing.Events;

using System.Collections.Generic;

namespace Ark.Tools.EventSourcing.Store
{
    public interface IOutboxEvent
    {
        string Id { get; }
        Dictionary<string, string> Metadata { get; }
        void SetEvent(object @event);
    }


    public interface IOutboxEvent<TEvent> : IOutboxEvent
        where TEvent : class, IDomainEvent
    {
        TEvent? Event { get; }
    }

}
