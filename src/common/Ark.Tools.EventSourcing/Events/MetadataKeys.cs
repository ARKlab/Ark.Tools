
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.EventSourcing(net10.0)', Before:
namespace Ark.Tools.EventSourcing.Events
{
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

        public const string UserId = "$user_id";
        public const string OperationId = "$operation_id";
    }
=======
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

    public const string UserId = "$user_id";
    public const string OperationId = "$operation_id";
>>>>>>> After
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

        public const string UserId = "$user_id";
        public const string OperationId = "$operation_id";
    }