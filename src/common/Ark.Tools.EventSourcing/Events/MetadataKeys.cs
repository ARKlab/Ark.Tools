#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.EventSourcing.Events;

public sealed class MetadataKeys
{
    public const string EventId = "$event_id";
    public const string BatchId = "$batch_id";
    public const string EventName = "$event_name";
    public const string EventVersion = "$event_version";
    public const string Timestamp = "$timestamp";
    public const string TimestampEpoch = "$timestamp_epoch";
    public const string AggregateVersion = "$aggregate_version";
    public const string AggregateName = "$aggregate_name";
    public const string AggregateId = "$aggregate_id";

    #if NET10_0_OR_GREATER

    [NotPersonalData("Metadata key label for a user identifier; it is not the personal value itself.")]

    #endif
    public const string UserId = "$user_id";
    public const string OperationId = "$operation_id";
}