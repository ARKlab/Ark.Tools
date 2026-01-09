using System;
using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
    public class Metadata : IMetadata
    {
        private readonly MetadataContainer _container;

        public Metadata()
            : this(new MetadataContainer())
        { }

        public Metadata(MetadataContainer container)
        {
            _container = container;
        }

        public string EventId
        {
            get => _container.GetMetadataValue(MetadataKeys.EventId) ?? throw new InvalidOperationException(MetadataKeys.EventId + " cannot be null");
            set => _container[MetadataKeys.EventId] = value;
        }

        public string EventName
        {
            get => _container.GetMetadataValue(MetadataKeys.EventName) ?? throw new InvalidOperationException(MetadataKeys.EventName + " cannot be null");
            set => _container[MetadataKeys.EventName] = value;
        }

        public int EventVersion
        {
            get => _container.GetMetadataValue(MetadataKeys.EventVersion, Convert.ToInt32) ?? throw new InvalidOperationException(MetadataKeys.EventVersion + " cannot be null");
            set => _container.SetMetadataValue<int>(MetadataKeys.EventVersion, value, Convert.ToString);
        }

        public DateTimeOffset Timestamp
        {
            get => _container.GetMetadataValue(MetadataKeys.Timestamp, DateTimeOffset.Parse) ?? throw new InvalidOperationException(MetadataKeys.Timestamp + " cannot be null");
            set => _container.SetMetadataValue<DateTimeOffset>(MetadataKeys.Timestamp, value, v => v.ToString("O"));
        }

        public long TimestampEpoch
        {
            get => _container.GetMetadataValue(MetadataKeys.TimestampEpoch, Convert.ToInt64) ?? throw new InvalidOperationException(MetadataKeys.TimestampEpoch + " cannot be null");
            set => _container.SetMetadataValue<long>(MetadataKeys.TimestampEpoch, value, Convert.ToString);
        }

        public long AggregateVersion
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateVersion, Convert.ToInt64) ?? throw new InvalidOperationException(MetadataKeys.AggregateVersion + " cannot be null");
            set => _container.SetMetadataValue<long>(MetadataKeys.AggregateVersion, value, Convert.ToString);
        }

        public string AggregateId
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateId) ?? throw new InvalidOperationException(MetadataKeys.AggregateId + " cannot be null");
            set => _container.SetMetadataValue(MetadataKeys.AggregateId, value);
        }

        public string AggregateName
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateName) ?? throw new InvalidOperationException(MetadataKeys.AggregateName + " cannot be null");
            set => _container.SetMetadataValue(MetadataKeys.AggregateName, value);
        }

        public IReadOnlyDictionary<string, string> Values => _container;

        public IMetadata CloneWith(params KeyValuePair<string, string>[] keyValuePairs)
            => CloneWith((IEnumerable<KeyValuePair<string, string>>)keyValuePairs);

        public IMetadata CloneWith(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            var container = new MetadataContainer(_container);
            foreach (var kv in keyValuePairs)
                container[kv.Key] = kv.Value;

            return new Metadata(container);
        }
    }


=======
namespace Ark.Tools.EventSourcing.Events;

public class Metadata : IMetadata
{
    private readonly MetadataContainer _container;

    public Metadata()
        : this(new MetadataContainer())
    { }

    public Metadata(MetadataContainer container)
    {
        _container = container;
    }

    public string EventId
    {
        get => _container.GetMetadataValue(MetadataKeys.EventId) ?? throw new InvalidOperationException(MetadataKeys.EventId + " cannot be null");
        set => _container[MetadataKeys.EventId] = value;
    }

    public string EventName
    {
        get => _container.GetMetadataValue(MetadataKeys.EventName) ?? throw new InvalidOperationException(MetadataKeys.EventName + " cannot be null");
        set => _container[MetadataKeys.EventName] = value;
    }

    public int EventVersion
    {
        get => _container.GetMetadataValue(MetadataKeys.EventVersion, Convert.ToInt32) ?? throw new InvalidOperationException(MetadataKeys.EventVersion + " cannot be null");
        set => _container.SetMetadataValue<int>(MetadataKeys.EventVersion, value, Convert.ToString);
    }

    public DateTimeOffset Timestamp
    {
        get => _container.GetMetadataValue(MetadataKeys.Timestamp, DateTimeOffset.Parse) ?? throw new InvalidOperationException(MetadataKeys.Timestamp + " cannot be null");
        set => _container.SetMetadataValue<DateTimeOffset>(MetadataKeys.Timestamp, value, v => v.ToString("O"));
    }

    public long TimestampEpoch
    {
        get => _container.GetMetadataValue(MetadataKeys.TimestampEpoch, Convert.ToInt64) ?? throw new InvalidOperationException(MetadataKeys.TimestampEpoch + " cannot be null");
        set => _container.SetMetadataValue<long>(MetadataKeys.TimestampEpoch, value, Convert.ToString);
    }

    public long AggregateVersion
    {
        get => _container.GetMetadataValue(MetadataKeys.AggregateVersion, Convert.ToInt64) ?? throw new InvalidOperationException(MetadataKeys.AggregateVersion + " cannot be null");
        set => _container.SetMetadataValue<long>(MetadataKeys.AggregateVersion, value, Convert.ToString);
    }

    public string AggregateId
    {
        get => _container.GetMetadataValue(MetadataKeys.AggregateId) ?? throw new InvalidOperationException(MetadataKeys.AggregateId + " cannot be null");
        set => _container.SetMetadataValue(MetadataKeys.AggregateId, value);
    }

    public string AggregateName
    {
        get => _container.GetMetadataValue(MetadataKeys.AggregateName) ?? throw new InvalidOperationException(MetadataKeys.AggregateName + " cannot be null");
        set => _container.SetMetadataValue(MetadataKeys.AggregateName, value);
    }

    public IReadOnlyDictionary<string, string> Values => _container;

    public IMetadata CloneWith(params KeyValuePair<string, string>[] keyValuePairs)
        => CloneWith((IEnumerable<KeyValuePair<string, string>>)keyValuePairs);

    public IMetadata CloneWith(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
    {
        var container = new MetadataContainer(_container);
        foreach (var kv in keyValuePairs)
            container[kv.Key] = kv.Value;

        return new Metadata(container);
    }
>>>>>>> After
    namespace Ark.Tools.EventSourcing.Events;

    public class Metadata : IMetadata
    {
        private readonly MetadataContainer _container;

        public Metadata()
            : this(new MetadataContainer())
        { }

        public Metadata(MetadataContainer container)
        {
            _container = container;
        }

        public string EventId
        {
            get => _container.GetMetadataValue(MetadataKeys.EventId) ?? throw new InvalidOperationException(MetadataKeys.EventId + " cannot be null");
            set => _container[MetadataKeys.EventId] = value;
        }

        public string EventName
        {
            get => _container.GetMetadataValue(MetadataKeys.EventName) ?? throw new InvalidOperationException(MetadataKeys.EventName + " cannot be null");
            set => _container[MetadataKeys.EventName] = value;
        }

        public int EventVersion
        {
            get => _container.GetMetadataValue(MetadataKeys.EventVersion, Convert.ToInt32) ?? throw new InvalidOperationException(MetadataKeys.EventVersion + " cannot be null");
            set => _container.SetMetadataValue<int>(MetadataKeys.EventVersion, value, Convert.ToString);
        }

        public DateTimeOffset Timestamp
        {
            get => _container.GetMetadataValue(MetadataKeys.Timestamp, DateTimeOffset.Parse) ?? throw new InvalidOperationException(MetadataKeys.Timestamp + " cannot be null");
            set => _container.SetMetadataValue<DateTimeOffset>(MetadataKeys.Timestamp, value, v => v.ToString("O"));
        }

        public long TimestampEpoch
        {
            get => _container.GetMetadataValue(MetadataKeys.TimestampEpoch, Convert.ToInt64) ?? throw new InvalidOperationException(MetadataKeys.TimestampEpoch + " cannot be null");
            set => _container.SetMetadataValue<long>(MetadataKeys.TimestampEpoch, value, Convert.ToString);
        }

        public long AggregateVersion
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateVersion, Convert.ToInt64) ?? throw new InvalidOperationException(MetadataKeys.AggregateVersion + " cannot be null");
            set => _container.SetMetadataValue<long>(MetadataKeys.AggregateVersion, value, Convert.ToString);
        }

        public string AggregateId
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateId) ?? throw new InvalidOperationException(MetadataKeys.AggregateId + " cannot be null");
            set => _container.SetMetadataValue(MetadataKeys.AggregateId, value);
        }

        public string AggregateName
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateName) ?? throw new InvalidOperationException(MetadataKeys.AggregateName + " cannot be null");
            set => _container.SetMetadataValue(MetadataKeys.AggregateName, value);
        }

        public IReadOnlyDictionary<string, string> Values => _container;

        public IMetadata CloneWith(params KeyValuePair<string, string>[] keyValuePairs)
            => CloneWith((IEnumerable<KeyValuePair<string, string>>)keyValuePairs);

        public IMetadata CloneWith(IEnumerable<KeyValuePair<string, string>> keyValuePairs)
        {
            var container = new MetadataContainer(_container);
            foreach (var kv in keyValuePairs)
                container[kv.Key] = kv.Value;

            return new Metadata(container);
        }
    }