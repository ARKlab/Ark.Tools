// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
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
=======
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
>>>>>>> After


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