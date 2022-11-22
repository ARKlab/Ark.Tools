using System;
using System.Collections.Generic;

namespace Ark.Tools.EventSourcing.Events
{
    public class Metadata : IMetadata
    {
        private MetadataContainer _container;

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

        public int? EventVersion
        {
            get => _container.GetMetadataValue(MetadataKeys.EventVersion, Convert.ToInt32);
            set => _container.SetMetadataValue(MetadataKeys.EventVersion, value, Convert.ToString);
        }

        public DateTimeOffset? Timestamp
        {
            get => _container.GetMetadataValue(MetadataKeys.Timestamp, DateTimeOffset.Parse);
            set => _container.SetMetadataValue(MetadataKeys.Timestamp, value, v => v.ToString("O"));
        }

        public long? TimestampEpoch
        {
            get => _container.GetMetadataValue(MetadataKeys.TimestampEpoch, Convert.ToInt64);
            set => _container.SetMetadataValue(MetadataKeys.TimestampEpoch, value, Convert.ToString);
        }

        public long? AggregateVersion
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateVersion, Convert.ToInt64);
            set => _container.SetMetadataValue(MetadataKeys.AggregateVersion, value, Convert.ToString);
        }

        public string? AggregateId
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateId);
            set => _container.SetMetadataValue(MetadataKeys.AggregateId, value);
        }

        public string? AggregateName
        {
            get => _container.GetMetadataValue(MetadataKeys.AggregateName);
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



}
