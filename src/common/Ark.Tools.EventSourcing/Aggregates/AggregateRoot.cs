using Ark.Tools.EventSourcing.Events;
using Ark.Tools.EventSourcing.Store;


using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Aggregates
{
    public abstract class AggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
        : IAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        public TAggregateRoot Aggregate { get; private set; }
        public string Identifier { get; }

        public IEnumerable<AggregateEventEnvelope<TAggregate>> History { get; private set; } = Enumerable.Empty<AggregateEventEnvelope<TAggregate>>();


        protected AggregateTransaction(string identifier, IAggregateRootFactory aggregateRootFactory)
        {
            Identifier = identifier;
            Aggregate = aggregateRootFactory.Create<TAggregateRoot>();
        }

        private void _createFromState(TAggregateState state)
        {
            Aggregate.SetState(state);
        }

        private void _createFromHistory(IEnumerable<AggregateEventEnvelope<TAggregate>> history, TAggregateState? snapshot = null)
        {
            if (snapshot != null)
                Aggregate.SetState(snapshot);
            else
                Aggregate.SetState(new TAggregateState
                {
                    Identifier = Identifier,
                    Version = 0
                });

            Aggregate.ApplyHistory(history);
        }

        public async Task LoadAsync(long maxVersion, CancellationToken ctk = default)
        {
            var events = await LoadHistory(maxVersion, ctk).ConfigureAwait(false);
            History = events;

            _createFromHistory(events);
        }

        public Task LoadAsync(CancellationToken ctk = default)
        {
            return LoadAsync(long.MaxValue, ctk);
        }

        public abstract Task<IEnumerable<AggregateEventEnvelope<TAggregate>>> LoadHistory(long maxVersion, CancellationToken ctk = default);
        public abstract Task SaveChangesAsync(CancellationToken ctk = default);

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public abstract class AggregateRoot<TAggregateRoot, TAggregateState, TAggregate> : IAggregateRoot
        where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        private static readonly IReadOnlyDictionary<Type, Action<TAggregateRoot, IAggregateEvent<TAggregate>, IMetadata>> _applyMethods;
        private static readonly string _aggregateName = AggregateHelper<TAggregate>.Name;

        private readonly List<AggregateEventEnvelope<TAggregate>> _uncommittedAggregateEvents = new();
        private readonly List<DomainEventEnvelope> _uncommittedDomainEvents = new();
        private bool _applying;

        public TAggregateState? State { get; private set; }

        public string Name => _aggregateName;
        public string Identifier => State!.Identifier;
        public long Version => State!.Version;
        public bool IsNew => Version == 0;
        public IEnumerable<AggregateEventEnvelope<TAggregate>> UncommittedAggregateEvents => _uncommittedAggregateEvents;
        public IEnumerable<DomainEventEnvelope> UncommittedDomainEvents => _uncommittedDomainEvents;

        static AggregateRoot()
        {
            var aggregateEventType = typeof(IAggregateEvent<TAggregate>);
            var aggregateStateType = typeof(TAggregateState);

            var methods = typeof(TAggregateRoot)
                .GetTypeInfo()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi => mi.Name == "Apply")
                .Where(mi =>
                {
                    var parameters = mi.GetParameters();
                    return parameters.Length == 2
                        && aggregateEventType.GetTypeInfo().IsAssignableFrom(parameters[0].ParameterType)
                        ;
                })
                ;

            _applyMethods = methods
                .ToDictionary(
                    mi => mi.GetParameters()[0].ParameterType,
                    mi =>
                    {
                        var eventType = mi.GetParameters()[0].ParameterType;
                        var aggregateParam = Expression.Parameter(typeof(TAggregateRoot), "agg");
                        var eventParam = Expression.Parameter(aggregateEventType, "evt");
                        var metadataParam = Expression.Parameter(typeof(IMetadata), "metadata");

                        var lambda = Expression.Lambda<Action<TAggregateRoot, IAggregateEvent<TAggregate>, IMetadata>>(
                            Expression.Call(
                                aggregateParam,
                                mi,
                                Expression.Convert(eventParam, eventType),
                                metadataParam
                            ),
                            aggregateParam,
                            eventParam,
                            metadataParam
                        );

                        return lambda.Compile();
                    }
                );
        }

        internal void SetState(TAggregateState state)
        {
            if (State != null)
                throw new InvalidOperationException("An used aggregate cannot change state");

            State = state;
            State._isRootManaged = true;
        }

        internal void ApplyHistory(IEnumerable<AggregateEventEnvelope<TAggregate>> history)
        {
            foreach (var e in history)
                _apply(e);
        }

        protected virtual void Emit<TEvent>(TEvent aggregateEvent, IDictionary<string, string>? metadata = null)
            where TEvent : class, IAggregateEvent<TAggregate>
        {
            ArgumentNullException.ThrowIfNull(aggregateEvent);

            if (_applying)
                throw new InvalidOperationException("Emit shall not be called during Apply phase. Do it before or after Emitting an event");

            var aggregateSequenceNumber = Version + 1;
            var eventId = $"{Name}/{Identifier}/{aggregateSequenceNumber:D20}";
            var now = DateTimeOffset.UtcNow;

            IMetadata eventMetadata = new Metadata
            {
                Timestamp = now,
                AggregateVersion = aggregateSequenceNumber,
                AggregateName = Name,
                AggregateId = Identifier,
                EventId = eventId,
                EventName = AggregateHelper<TAggregate>.EventHelper<TEvent>.Name,
                //EventVersion = AggregateHelper<TAggregate>.EventHelper<TEvent>.Version,
                TimestampEpoch = now.ToUnixTimeMilliseconds()
            };

            if (metadata != null)
            {
                eventMetadata = eventMetadata.CloneWith(metadata);
            }

            var uncommittedEvent = new AggregateEventEnvelope<TAggregate>(aggregateEvent, eventMetadata);

            _apply(uncommittedEvent);

            _uncommittedAggregateEvents.Add(uncommittedEvent);
        }

        protected virtual void Publish<TEvent>(TEvent domainEvent, IMetadata? metadata = null)
            where TEvent : class, IDomainEvent
        {
            ArgumentNullException.ThrowIfNull(domainEvent);

            if (_applying)
                throw new InvalidOperationException("Publish shall not be called during Apply phase. Do it before or after Emitting an event");

            var now = DateTimeOffset.UtcNow;
            var eventMetadata = new Metadata
            {
                Timestamp = now,
                EventId = Guid.NewGuid().ToString(),
                TimestampEpoch = now.ToUnixTimeMilliseconds(),

                AggregateVersion = Version,
                AggregateName = Name,
                AggregateId = Identifier,
            };

            if (metadata != null)
            {
                eventMetadata.CloneWith(metadata.Values);
            }

            _uncommittedDomainEvents.Add(new DomainEventEnvelope(domainEvent, eventMetadata));
        }

        private void _apply(AggregateEventEnvelope<TAggregate> aggregateEvent)
        {
            _applying = true;
            var eventType = aggregateEvent.Event.GetType();

            if (!_applyMethods.TryGetValue(eventType, out var applyMethod))
            {
                throw new InvalidOperationException(
                    $"Aggregate '{Name}' does have an 'Apply' method that takes aggregate event '{eventType}' as argument");
            }

            applyMethod((TAggregateRoot)this, aggregateEvent.Event, aggregateEvent.Metadata);

            ValidateInvariantsOrThrow();

            ++State!._version;
            _applying = false;
        }


        public void Commit()
        {
            _uncommittedAggregateEvents.Clear();
        }

        protected virtual void ValidateInvariantsOrThrow()
        {
        }
=======
namespace Ark.Tools.EventSourcing.Aggregates;

public abstract class AggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
    : IAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
    where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
    where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
    where TAggregate : IAggregate
{
    public TAggregateRoot Aggregate { get; private set; }
    public string Identifier { get; }

    public IEnumerable<AggregateEventEnvelope<TAggregate>> History { get; private set; } = Enumerable.Empty<AggregateEventEnvelope<TAggregate>>();


    protected AggregateTransaction(string identifier, IAggregateRootFactory aggregateRootFactory)
    {
        Identifier = identifier;
        Aggregate = aggregateRootFactory.Create<TAggregateRoot>();
    }

    private void _createFromState(TAggregateState state)
    {
        Aggregate.SetState(state);
    }

    private void _createFromHistory(IEnumerable<AggregateEventEnvelope<TAggregate>> history, TAggregateState? snapshot = null)
    {
        if (snapshot != null)
            Aggregate.SetState(snapshot);
        else
            Aggregate.SetState(new TAggregateState
            {
                Identifier = Identifier,
                Version = 0
            });

        Aggregate.ApplyHistory(history);
    }

    public async Task LoadAsync(long maxVersion, CancellationToken ctk = default)
    {
        var events = await LoadHistory(maxVersion, ctk).ConfigureAwait(false);
        History = events;

        _createFromHistory(events);
    }

    public Task LoadAsync(CancellationToken ctk = default)
    {
        return LoadAsync(long.MaxValue, ctk);
    }

    public abstract Task<IEnumerable<AggregateEventEnvelope<TAggregate>>> LoadHistory(long maxVersion, CancellationToken ctk = default);
    public abstract Task SaveChangesAsync(CancellationToken ctk = default);

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public abstract class AggregateRoot<TAggregateRoot, TAggregateState, TAggregate> : IAggregateRoot
    where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
    where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
    where TAggregate : IAggregate
{
    private static readonly IReadOnlyDictionary<Type, Action<TAggregateRoot, IAggregateEvent<TAggregate>, IMetadata>> _applyMethods;
    private static readonly string _aggregateName = AggregateHelper<TAggregate>.Name;

    private readonly List<AggregateEventEnvelope<TAggregate>> _uncommittedAggregateEvents = new();
    private readonly List<DomainEventEnvelope> _uncommittedDomainEvents = new();
    private bool _applying;

    public TAggregateState? State { get; private set; }

    public string Name => _aggregateName;
    public string Identifier => State!.Identifier;
    public long Version => State!.Version;
    public bool IsNew => Version == 0;
    public IEnumerable<AggregateEventEnvelope<TAggregate>> UncommittedAggregateEvents => _uncommittedAggregateEvents;
    public IEnumerable<DomainEventEnvelope> UncommittedDomainEvents => _uncommittedDomainEvents;

    static AggregateRoot()
    {
        var aggregateEventType = typeof(IAggregateEvent<TAggregate>);
        var aggregateStateType = typeof(TAggregateState);

        var methods = typeof(TAggregateRoot)
            .GetTypeInfo()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(mi => mi.Name == "Apply")
            .Where(mi =>
            {
                var parameters = mi.GetParameters();
                return parameters.Length == 2
                    && aggregateEventType.GetTypeInfo().IsAssignableFrom(parameters[0].ParameterType)
                    ;
            })
            ;

        _applyMethods = methods
            .ToDictionary(
                mi => mi.GetParameters()[0].ParameterType,
                mi =>
                {
                    var eventType = mi.GetParameters()[0].ParameterType;
                    var aggregateParam = Expression.Parameter(typeof(TAggregateRoot), "agg");
                    var eventParam = Expression.Parameter(aggregateEventType, "evt");
                    var metadataParam = Expression.Parameter(typeof(IMetadata), "metadata");

                    var lambda = Expression.Lambda<Action<TAggregateRoot, IAggregateEvent<TAggregate>, IMetadata>>(
                        Expression.Call(
                            aggregateParam,
                            mi,
                            Expression.Convert(eventParam, eventType),
                            metadataParam
                        ),
                        aggregateParam,
                        eventParam,
                        metadataParam
                    );

                    return lambda.Compile();
                }
            );
    }

    internal void SetState(TAggregateState state)
    {
        if (State != null)
            throw new InvalidOperationException("An used aggregate cannot change state");

        State = state;
        State._isRootManaged = true;
    }

    internal void ApplyHistory(IEnumerable<AggregateEventEnvelope<TAggregate>> history)
    {
        foreach (var e in history)
            _apply(e);
    }

    protected virtual void Emit<TEvent>(TEvent aggregateEvent, IDictionary<string, string>? metadata = null)
        where TEvent : class, IAggregateEvent<TAggregate>
    {
        ArgumentNullException.ThrowIfNull(aggregateEvent);

        if (_applying)
            throw new InvalidOperationException("Emit shall not be called during Apply phase. Do it before or after Emitting an event");

        var aggregateSequenceNumber = Version + 1;
        var eventId = $"{Name}/{Identifier}/{aggregateSequenceNumber:D20}";
        var now = DateTimeOffset.UtcNow;

        IMetadata eventMetadata = new Metadata
        {
            Timestamp = now,
            AggregateVersion = aggregateSequenceNumber,
            AggregateName = Name,
            AggregateId = Identifier,
            EventId = eventId,
            EventName = AggregateHelper<TAggregate>.EventHelper<TEvent>.Name,
            //EventVersion = AggregateHelper<TAggregate>.EventHelper<TEvent>.Version,
            TimestampEpoch = now.ToUnixTimeMilliseconds()
        };

        if (metadata != null)
        {
            eventMetadata = eventMetadata.CloneWith(metadata);
        }

        var uncommittedEvent = new AggregateEventEnvelope<TAggregate>(aggregateEvent, eventMetadata);

        _apply(uncommittedEvent);

        _uncommittedAggregateEvents.Add(uncommittedEvent);
    }

    protected virtual void Publish<TEvent>(TEvent domainEvent, IMetadata? metadata = null)
        where TEvent : class, IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (_applying)
            throw new InvalidOperationException("Publish shall not be called during Apply phase. Do it before or after Emitting an event");

        var now = DateTimeOffset.UtcNow;
        var eventMetadata = new Metadata
        {
            Timestamp = now,
            EventId = Guid.NewGuid().ToString(),
            TimestampEpoch = now.ToUnixTimeMilliseconds(),

            AggregateVersion = Version,
            AggregateName = Name,
            AggregateId = Identifier,
        };

        if (metadata != null)
        {
            eventMetadata.CloneWith(metadata.Values);
        }

        _uncommittedDomainEvents.Add(new DomainEventEnvelope(domainEvent, eventMetadata));
    }

    private void _apply(AggregateEventEnvelope<TAggregate> aggregateEvent)
    {
        _applying = true;
        var eventType = aggregateEvent.Event.GetType();

        if (!_applyMethods.TryGetValue(eventType, out var applyMethod))
        {
            throw new InvalidOperationException(
                $"Aggregate '{Name}' does have an 'Apply' method that takes aggregate event '{eventType}' as argument");
        }

        applyMethod((TAggregateRoot)this, aggregateEvent.Event, aggregateEvent.Metadata);

        ValidateInvariantsOrThrow();

        ++State!._version;
        _applying = false;
    }


    public void Commit()
    {
        _uncommittedAggregateEvents.Clear();
    }

    protected virtual void ValidateInvariantsOrThrow()
    {
>>>>>>> After


namespace Ark.Tools.EventSourcing.Aggregates;

    public abstract class AggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
        : IAggregateTransaction<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        public TAggregateRoot Aggregate { get; private set; }
        public string Identifier { get; }

        public IEnumerable<AggregateEventEnvelope<TAggregate>> History { get; private set; } = Enumerable.Empty<AggregateEventEnvelope<TAggregate>>();


        protected AggregateTransaction(string identifier, IAggregateRootFactory aggregateRootFactory)
        {
            Identifier = identifier;
            Aggregate = aggregateRootFactory.Create<TAggregateRoot>();
        }

        private void _createFromState(TAggregateState state)
        {
            Aggregate.SetState(state);
        }

        private void _createFromHistory(IEnumerable<AggregateEventEnvelope<TAggregate>> history, TAggregateState? snapshot = null)
        {
            if (snapshot != null)
                Aggregate.SetState(snapshot);
            else
                Aggregate.SetState(new TAggregateState
                {
                    Identifier = Identifier,
                    Version = 0
                });

            Aggregate.ApplyHistory(history);
        }

        public async Task LoadAsync(long maxVersion, CancellationToken ctk = default)
        {
            var events = await LoadHistory(maxVersion, ctk).ConfigureAwait(false);
            History = events;

            _createFromHistory(events);
        }

        public Task LoadAsync(CancellationToken ctk = default)
        {
            return LoadAsync(long.MaxValue, ctk);
        }

        public abstract Task<IEnumerable<AggregateEventEnvelope<TAggregate>>> LoadHistory(long maxVersion, CancellationToken ctk = default);
        public abstract Task SaveChangesAsync(CancellationToken ctk = default);

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public abstract class AggregateRoot<TAggregateRoot, TAggregateState, TAggregate> : IAggregateRoot
        where TAggregateRoot : AggregateRoot<TAggregateRoot, TAggregateState, TAggregate>
        where TAggregateState : AggregateState<TAggregateState, TAggregate>, new()
        where TAggregate : IAggregate
    {
        private static readonly IReadOnlyDictionary<Type, Action<TAggregateRoot, IAggregateEvent<TAggregate>, IMetadata>> _applyMethods;
        private static readonly string _aggregateName = AggregateHelper<TAggregate>.Name;

        private readonly List<AggregateEventEnvelope<TAggregate>> _uncommittedAggregateEvents = new();
        private readonly List<DomainEventEnvelope> _uncommittedDomainEvents = new();
        private bool _applying;

        public TAggregateState? State { get; private set; }

        public string Name => _aggregateName;
        public string Identifier => State!.Identifier;
        public long Version => State!.Version;
        public bool IsNew => Version == 0;
        public IEnumerable<AggregateEventEnvelope<TAggregate>> UncommittedAggregateEvents => _uncommittedAggregateEvents;
        public IEnumerable<DomainEventEnvelope> UncommittedDomainEvents => _uncommittedDomainEvents;

        static AggregateRoot()
        {
            var aggregateEventType = typeof(IAggregateEvent<TAggregate>);
            var aggregateStateType = typeof(TAggregateState);

            var methods = typeof(TAggregateRoot)
                .GetTypeInfo()
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(mi => mi.Name == "Apply")
                .Where(mi =>
                {
                    var parameters = mi.GetParameters();
                    return parameters.Length == 2
                        && aggregateEventType.GetTypeInfo().IsAssignableFrom(parameters[0].ParameterType)
                        ;
                })
                ;

            _applyMethods = methods
                .ToDictionary(
                    mi => mi.GetParameters()[0].ParameterType,
                    mi =>
                    {
                        var eventType = mi.GetParameters()[0].ParameterType;
                        var aggregateParam = Expression.Parameter(typeof(TAggregateRoot), "agg");
                        var eventParam = Expression.Parameter(aggregateEventType, "evt");
                        var metadataParam = Expression.Parameter(typeof(IMetadata), "metadata");

                        var lambda = Expression.Lambda<Action<TAggregateRoot, IAggregateEvent<TAggregate>, IMetadata>>(
                            Expression.Call(
                                aggregateParam,
                                mi,
                                Expression.Convert(eventParam, eventType),
                                metadataParam
                            ),
                            aggregateParam,
                            eventParam,
                            metadataParam
                        );

                        return lambda.Compile();
                    }
                );
        }

        internal void SetState(TAggregateState state)
        {
            if (State != null)
                throw new InvalidOperationException("An used aggregate cannot change state");

            State = state;
            State._isRootManaged = true;
        }

        internal void ApplyHistory(IEnumerable<AggregateEventEnvelope<TAggregate>> history)
        {
            foreach (var e in history)
                _apply(e);
        }

        protected virtual void Emit<TEvent>(TEvent aggregateEvent, IDictionary<string, string>? metadata = null)
            where TEvent : class, IAggregateEvent<TAggregate>
        {
            ArgumentNullException.ThrowIfNull(aggregateEvent);

            if (_applying)
                throw new InvalidOperationException("Emit shall not be called during Apply phase. Do it before or after Emitting an event");

            var aggregateSequenceNumber = Version + 1;
            var eventId = $"{Name}/{Identifier}/{aggregateSequenceNumber:D20}";
            var now = DateTimeOffset.UtcNow;

            IMetadata eventMetadata = new Metadata
            {
                Timestamp = now,
                AggregateVersion = aggregateSequenceNumber,
                AggregateName = Name,
                AggregateId = Identifier,
                EventId = eventId,
                EventName = AggregateHelper<TAggregate>.EventHelper<TEvent>.Name,
                //EventVersion = AggregateHelper<TAggregate>.EventHelper<TEvent>.Version,
                TimestampEpoch = now.ToUnixTimeMilliseconds()
            };

            if (metadata != null)
            {
                eventMetadata = eventMetadata.CloneWith(metadata);
            }

            var uncommittedEvent = new AggregateEventEnvelope<TAggregate>(aggregateEvent, eventMetadata);

            _apply(uncommittedEvent);

            _uncommittedAggregateEvents.Add(uncommittedEvent);
        }

        protected virtual void Publish<TEvent>(TEvent domainEvent, IMetadata? metadata = null)
            where TEvent : class, IDomainEvent
        {
            ArgumentNullException.ThrowIfNull(domainEvent);

            if (_applying)
                throw new InvalidOperationException("Publish shall not be called during Apply phase. Do it before or after Emitting an event");

            var now = DateTimeOffset.UtcNow;
            var eventMetadata = new Metadata
            {
                Timestamp = now,
                EventId = Guid.NewGuid().ToString(),
                TimestampEpoch = now.ToUnixTimeMilliseconds(),

                AggregateVersion = Version,
                AggregateName = Name,
                AggregateId = Identifier,
            };

            if (metadata != null)
            {
                eventMetadata.CloneWith(metadata.Values);
            }

            _uncommittedDomainEvents.Add(new DomainEventEnvelope(domainEvent, eventMetadata));
        }

        private void _apply(AggregateEventEnvelope<TAggregate> aggregateEvent)
        {
            _applying = true;
            var eventType = aggregateEvent.Event.GetType();

            if (!_applyMethods.TryGetValue(eventType, out var applyMethod))
            {
                throw new InvalidOperationException(
                    $"Aggregate '{Name}' does have an 'Apply' method that takes aggregate event '{eventType}' as argument");
            }

            applyMethod((TAggregateRoot)this, aggregateEvent.Event, aggregateEvent.Metadata);

            ValidateInvariantsOrThrow();

            ++State!._version;
            _applying = false;
        }


        public void Commit()
        {
            _uncommittedAggregateEvents.Clear();
        }

        protected virtual void ValidateInvariantsOrThrow()
        {
        }
    }