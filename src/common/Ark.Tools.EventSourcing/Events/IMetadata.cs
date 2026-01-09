using System;
using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
    public interface IMetadata
    {
        string EventId { get; }
        string EventName { get; }
        int EventVersion { get; }
        DateTimeOffset Timestamp { get; }
        long TimestampEpoch { get; }
        long AggregateVersion { get; }
        string AggregateId { get; }
        string AggregateName { get; }

        IReadOnlyDictionary<string, string> Values { get; }

        IMetadata CloneWith(params KeyValuePair<string, string>[] keyValuePairs);
        IMetadata CloneWith(IEnumerable<KeyValuePair<string, string>> keyValuePairs);
    }
=======
namespace Ark.Tools.EventSourcing.Events;

public interface IMetadata
{
    string EventId { get; }
    string EventName { get; }
    int EventVersion { get; }
    DateTimeOffset Timestamp { get; }
    long TimestampEpoch { get; }
    long AggregateVersion { get; }
    string AggregateId { get; }
    string AggregateName { get; }

    IReadOnlyDictionary<string, string> Values { get; }

    IMetadata CloneWith(params KeyValuePair<string, string>[] keyValuePairs);
    IMetadata CloneWith(IEnumerable<KeyValuePair<string, string>> keyValuePairs);
>>>>>>> After


namespace Ark.Tools.EventSourcing.Events;

public interface IMetadata
{
    string EventId { get; }
    string EventName { get; }
    int EventVersion { get; }
    DateTimeOffset Timestamp { get; }
    long TimestampEpoch { get; }
    long AggregateVersion { get; }
    string AggregateId { get; }
    string AggregateName { get; }

    IReadOnlyDictionary<string, string> Values { get; }

    IMetadata CloneWith(params KeyValuePair<string, string>[] keyValuePairs);
    IMetadata CloneWith(IEnumerable<KeyValuePair<string, string>> keyValuePairs);
}