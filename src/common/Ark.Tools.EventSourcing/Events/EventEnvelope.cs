// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tools.EventSourcing.Events;

public abstract class EventEnvelope<T> : IEventEnvelope<T>
    where T : class, IEvent
{
    public T Event { get; }
    public IMetadata Metadata { get; }

    protected EventEnvelope(T @event, IMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(@event);
        Event = @event;
        Metadata = metadata;
    }
}