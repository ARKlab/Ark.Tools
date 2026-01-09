using Ark.Tools.EventSourcing.Events;

using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
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
=======
namespace Ark.Tools.EventSourcing.Store;

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
>>>>>>> After


namespace Ark.Tools.EventSourcing.Store;

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