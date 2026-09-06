// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using NLog;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Collects incoming message metrics through OpenTelemetry.</summary>
public sealed class OpenTelemetryProcessingMetricsStep : IMessagingIncomingStep
{
    /// <summary>The stable meter name for MediatorFramework messaging metrics.</summary>
    public const string MeterName = OpenTelemetryIncomingStep.ActivitySourceName;

    private readonly IClock _clock;

    /// <summary>Creates a metrics step using the system clock.</summary>
    public OpenTelemetryProcessingMetricsStep()
        : this(SystemClock.Instance)
    {
    }

    /// <summary>Creates a metrics step using the supplied clock.</summary>
    /// <param name="clock">The clock used to calculate queue time.</param>
    public OpenTelemetryProcessingMetricsStep(IClock clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <inheritdoc />
    public async Task ProcessAsync(
        MessagingIncomingContext context,
        Func<Task> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var stopwatch = Stopwatch.StartNew();
        var outcome = "complete";
        try
        {
            await next().ConfigureAwait(false);
        }
        catch
        {
            outcome = "error";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            if (!context.Items.ContainsKey(MessagingMetrics._dispatcherManagedItem))
            {
                MessagingMetrics.RecordProcessing(
                    stopwatch.Elapsed,
                    context.Headers,
                    outcome,
                    context.DeliveryCount,
                    now: _clock.GetCurrentInstant().ToDateTimeOffset());
            }
        }
    }
}

/// <summary>Defines the stable OpenTelemetry messaging metric contract.</summary>
public static class MessagingMetrics
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
    /// <summary>The semantic-conventions version used by this contract.</summary>
    public const string SemanticConventionVersion = "1.37.0";
    /// <summary>The client operation duration instrument.</summary>
    public const string ClientOperationDurationName = "messaging.client.operation.duration";
    /// <summary>The process duration instrument.</summary>
    public const string ProcessDurationName = "messaging.process.duration";
    /// <summary>The time-in-queue instrument.</summary>
    public const string TimeInQueueName = "messaging.message.time_in_queue";
    /// <summary>The processed message outcome instrument.</summary>
    public const string ProcessedMessagesName = "messaging.process.messages";
    /// <summary>The native delivery-attempt instrument.</summary>
    public const string DeliveryAttemptsName = "messaging.process.attempts";
    /// <summary>The item key used to retain the processing start timestamp.</summary>
    internal const string _processingStartItem = "ark.messaging.metrics.processing-start";
    internal const string _dispatcherManagedItem = "ark.messaging.metrics.dispatcher-managed";

    /// <summary>The meter carrying the opt-in advanced tuning tier.</summary>
    public const string AdvancedMeterName = OpenTelemetryProcessingMetricsStep.MeterName + ".Advanced";
    /// <summary>The concurrency-limit instrument.</summary>
    public const string ConcurrencyLimitName = "messaging.process.concurrency.limit";
    /// <summary>The in-flight delivery instrument.</summary>
    public const string InFlightName = "messaging.process.in_flight";
    /// <summary>The locally buffered delivery instrument.</summary>
    public const string BufferedName = "messaging.process.buffered";
    /// <summary>The broker-throttling instrument.</summary>
    public const string ThrottledName = "messaging.process.throttled";
    /// <summary>The lock-renewal outcome instrument.</summary>
    public const string LockRenewalsName = "messaging.process.lock_renewals";
    /// <summary>The effective receive batch size instrument.</summary>
    public const string ReceiveBatchSizeName = "messaging.receive.batch.size";
    /// <summary>The empty-receive instrument.</summary>
    public const string ReceiveEmptyName = "messaging.receive.empty";
    /// <summary>The idle backoff interval instrument.</summary>
    public const string ReceiveBackoffIntervalName = "messaging.receive.backoff.interval";
    /// <summary>The fetch-to-handler wait instrument.</summary>
    public const string ProcessQueueWaitName = "messaging.process.queue_wait";
    /// <summary>The settlement duration instrument.</summary>
    public const string ProcessSettleDurationName = "messaging.process.settle.duration";
    /// <summary>The concurrency latency-gradient instrument.</summary>
    public const string ConcurrencyGradientName = "messaging.concurrency.gradient";
    /// <summary>The concurrency-decision instrument.</summary>
    public const string ConcurrencyDecisionName = "messaging.concurrency.decision";

    private static readonly Meter _meter = new(OpenTelemetryProcessingMetricsStep.MeterName);
    private static readonly Histogram<double> _clientOperationDuration =
        _meter.CreateHistogram<double>(ClientOperationDurationName, "s", "Duration of a messaging client operation.");
    private static readonly Histogram<double> _processDuration =
        _meter.CreateHistogram<double>(ProcessDurationName, "s", "Duration of message processing.");
    private static readonly Histogram<double> _timeInQueue =
        _meter.CreateHistogram<double>(TimeInQueueName, "s", "Duration a message waits in the queue.");
    private static readonly Counter<long> _processedMessages =
        _meter.CreateCounter<long>(ProcessedMessagesName, "{message}", "Number of messages processed.");
    private static readonly Histogram<double> _deliveryAttempts =
        _meter.CreateHistogram<double>(DeliveryAttemptsName, "{attempt}", "Native delivery attempts.");

    private static readonly UpDownCounter<int> _concurrencyLimit =
        _meter.CreateUpDownCounter<int>(ConcurrencyLimitName, "{worker}", "Current concurrency limit of the processor host.");
    private static readonly UpDownCounter<int> _inFlight =
        _meter.CreateUpDownCounter<int>(InFlightName, "{message}", "Deliveries currently being dispatched to handlers.");
    private static readonly UpDownCounter<int> _buffered =
        _meter.CreateUpDownCounter<int>(BufferedName, "{message}", "Deliveries prefetched and waiting for a worker.");
    private static readonly Counter<long> _throttled =
        _meter.CreateCounter<long>(ThrottledName, "{event}", "Broker throttling responses observed by the host.");
    private static readonly Counter<long> _lockRenewals =
        _meter.CreateCounter<long>(LockRenewalsName, "{renewal}", "Lock renewal attempts by outcome.");

    private static readonly Meter _advancedMeter = new(AdvancedMeterName);
    private static readonly Histogram<int> _receiveBatchSize =
        _advancedMeter.CreateHistogram<int>(ReceiveBatchSizeName, "{message}", "Deliveries returned by one receive call.");
    private static readonly Counter<long> _receiveEmpty =
        _advancedMeter.CreateCounter<long>(ReceiveEmptyName, "{receive}", "Receive calls that returned no delivery.");
    private static readonly Histogram<double> _receiveBackoffInterval =
        _advancedMeter.CreateHistogram<double>(ReceiveBackoffIntervalName, "s", "Idle wait applied between empty receives.");
    private static readonly Histogram<double> _processQueueWait =
        _advancedMeter.CreateHistogram<double>(ProcessQueueWaitName, "s", "Time a delivery waits in the local buffer.");
    private static readonly Histogram<double> _processSettleDuration =
        _advancedMeter.CreateHistogram<double>(ProcessSettleDurationName, "s", "Time spent settling a delivery.");
    private static readonly Histogram<double> _concurrencyGradient =
        _advancedMeter.CreateHistogram<double>(ConcurrencyGradientName, "1", "Latency gradient driving the concurrency brake.");
    private static readonly Counter<long> _concurrencyDecision =
        _advancedMeter.CreateCounter<long>(ConcurrencyDecisionName, "{decision}", "Concurrency limit changes by reason.");

    private static int _advancedRegistered;

    /// <summary>Gets whether the advanced tier was registered through its OpenTelemetry extension.</summary>
    internal static bool _advancedMetricsRegistered => Volatile.Read(ref _advancedRegistered) != 0;

    /// <summary>Enables the advanced tier process-wide, so a registered advanced meter is never empty.</summary>
    /// <remarks>Called by <c>AddArkMessagingAdvancedInstrumentation</c>; enabling is one-way by design.</remarks>
    public static void EnableAdvancedMetrics()
    {
        Volatile.Write(ref _advancedRegistered, 1);
    }

    internal static void _disableAdvancedMetrics()
    {
        Volatile.Write(ref _advancedRegistered, 0);
    }

    /// <summary>Builds the bounded topology tag set shared by both tiers.</summary>
    /// <param name="destination">The queue or topic name.</param>
    /// <param name="participant">The participant identity, when known.</param>
    /// <returns>The tag array, built once per host.</returns>
    internal static KeyValuePair<string, object?>[] _topologyTags(string destination, string? participant)
    {
        // Bounded topology only: no delivery ids, correlation ids or exception text may become a tag.
        var tags = new List<KeyValuePair<string, object?>>(3)
        {
            new("messaging.system", "ark.mediatorframework"),
            new("messaging.destination.name", destination),
        };
        if (!string.IsNullOrWhiteSpace(participant))
            tags.Add(new("ark.participant", participant));

        return tags.ToArray();
    }

    /// <summary>Adds a delta to the operational concurrency-limit gauge.</summary>
    /// <param name="delta">The signed change.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordConcurrencyLimit(int delta, KeyValuePair<string, object?>[] tags)
    {
        _add(_concurrencyLimit, delta, tags);
    }

    /// <summary>Adds a delta to the operational in-flight gauge.</summary>
    /// <param name="delta">The signed change.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordInFlight(int delta, KeyValuePair<string, object?>[] tags)
    {
        _add(_inFlight, delta, tags);
    }

    /// <summary>Adds a delta to the operational buffered gauge.</summary>
    /// <param name="delta">The signed change.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordBuffered(int delta, KeyValuePair<string, object?>[] tags)
    {
        _add(_buffered, delta, tags);
    }

    /// <summary>Counts one broker throttling response.</summary>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordThrottled(KeyValuePair<string, object?>[] tags)
    {
        if (!_throttled.Enabled)
            return;

        try
        {
            _throttled.Add(1, tags);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _warn(exception);
        }
    }

    /// <summary>Counts one lock renewal attempt by outcome.</summary>
    /// <param name="outcomeTags">The host topology tags already carrying the <c>outcome</c> tag.</param>
    internal static void _recordLockRenewal(KeyValuePair<string, object?>[] outcomeTags)
    {
        if (!_lockRenewals.Enabled)
            return;

        try
        {
            _lockRenewals.Add(1, outcomeTags);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _warn(exception);
        }
    }

    /// <summary>Records the number of deliveries returned by one receive call.</summary>
    /// <param name="received">The delivery count, zero for an empty receive.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordReceiveBatch(int received, KeyValuePair<string, object?>[] tags)
    {
        try
        {
            if (_receiveBatchSize.Enabled)
                _receiveBatchSize.Record(received, tags);
            if (received <= 0 && _receiveEmpty.Enabled)
                _receiveEmpty.Add(1, tags);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _warn(exception);
        }
    }

    /// <summary>Records the idle wait applied after an empty receive.</summary>
    /// <param name="interval">The wait interval.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordBackoffInterval(TimeSpan interval, KeyValuePair<string, object?>[] tags)
    {
        _record(_receiveBackoffInterval, interval.TotalSeconds, tags);
    }

    /// <summary>Records how long a delivery waited in the local buffer.</summary>
    /// <param name="wait">The wait duration.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordQueueWait(TimeSpan wait, KeyValuePair<string, object?>[] tags)
    {
        _record(_processQueueWait, wait.TotalSeconds, tags);
    }

    /// <summary>Records the cost of settling one delivery.</summary>
    /// <param name="duration">The settlement duration.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordSettleDuration(TimeSpan duration, KeyValuePair<string, object?>[] tags)
    {
        _record(_processSettleDuration, duration.TotalSeconds, tags);
    }

    /// <summary>Records the latency gradient of one control interval.</summary>
    /// <param name="gradient">The measured gradient.</param>
    /// <param name="tags">The host topology tags.</param>
    internal static void _recordGradient(double gradient, KeyValuePair<string, object?>[] tags)
    {
        _record(_concurrencyGradient, gradient, tags);
    }

    /// <summary>Counts one concurrency-limit change by reason.</summary>
    /// <param name="reasonTags">The host topology tags already carrying the <c>reason</c> tag.</param>
    internal static void _recordConcurrencyDecision(KeyValuePair<string, object?>[] reasonTags)
    {
        if (!_concurrencyDecision.Enabled)
            return;

        try
        {
            _concurrencyDecision.Add(1, reasonTags);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _warn(exception);
        }
    }

    private static void _add(UpDownCounter<int> instrument, int delta, KeyValuePair<string, object?>[] tags)
    {
        if (delta == 0 || !instrument.Enabled)
            return;

        try
        {
            instrument.Add(delta, tags);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _warn(exception);
        }
    }

    private static void _record(Histogram<double> instrument, double value, KeyValuePair<string, object?>[] tags)
    {
        if (!instrument.Enabled)
            return;

        try
        {
            instrument.Record(Math.Max(0, value), tags);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _warn(exception);
        }
    }

    private static void _warn(Exception exception)
    {
        _logger.Warn(
            exception,
            CultureInfo.InvariantCulture,
            "Messaging metric recording failed: {Message}",
            exception.Message);
    }

    /// <summary>Records a producer client operation duration.</summary>
    /// <param name="duration">The elapsed operation duration.</param>
    /// <param name="headers">The framework headers.</param>
    /// <param name="operation">The low-cardinality operation name.</param>
    /// <param name="destination">The destination name.</param>
    public static void RecordClientOperation(
        TimeSpan duration,
        IReadOnlyDictionary<string, string> headers,
        string operation,
        string destination)
    {
        if (!_clientOperationDuration.Enabled)
            return;

        try
        {
            _clientOperationDuration.Record(
                Math.Max(0, duration.TotalSeconds),
                _attributes(headers, operation, destination));
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            _logger.Warn(
                exception,
                System.Globalization.CultureInfo.InvariantCulture,
                "Messaging client metric recording failed: {Message}",
                exception.Message);
        }
    }

    /// <summary>Records processing duration, outcome, queue time, and delivery attempts.</summary>
    /// <param name="duration">The elapsed processing duration.</param>
    /// <param name="headers">The framework headers.</param>
    /// <param name="outcome">The final settlement outcome.</param>
    /// <param name="deliveryCount">The native delivery attempt count.</param>
    /// <param name="destination">The optional destination name.</param>
    /// <param name="now">The clock instant used for queue-time measurement.</param>
    public static void RecordProcessing(
        TimeSpan duration,
        IReadOnlyDictionary<string, string> headers,
        string outcome,
        int deliveryCount,
        string? destination = null,
        DateTimeOffset? now = null)
    {
        if (!_processDuration.Enabled
            && !_processedMessages.Enabled
            && !_deliveryAttempts.Enabled
            && !_timeInQueue.Enabled)
        {
            return;
        }

        try
        {
            var attributes = _attributes(headers, "process", destination);
            Array.Resize(ref attributes, attributes.Length + 1);
            attributes[^1] = new KeyValuePair<string, object?>("messaging.process.result", outcome);
            _processDuration.Record(Math.Max(0, duration.TotalSeconds), attributes);
            _processedMessages.Add(1, attributes);
            if (deliveryCount > 0)
                _deliveryAttempts.Record(deliveryCount, attributes);
            if (_tryGetSentTime(headers, out var sentTime) && now is not null)
                _timeInQueue.Record(Math.Max(0, (now.Value - sentTime - duration).TotalSeconds), attributes);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
            LogManager.GetCurrentClassLogger().Warn(
                exception,
                System.Globalization.CultureInfo.InvariantCulture,
                "Messaging processing metric recording failed: {Message}",
                exception.Message);
        }
    }

    private static KeyValuePair<string, object?>[] _attributes(
        IReadOnlyDictionary<string, string> headers,
        string operation,
        string? destination)
    {
        var attributes = new List<KeyValuePair<string, object?>>(6)
        {
            new("messaging.system", "ark.mediatorframework"),
            new("messaging.operation.name", operation),
        };
        if (!string.IsNullOrWhiteSpace(destination))
        {
            attributes.Add(new("messaging.destination.name", destination));
            attributes.Add(new(
                "messaging.destination.kind",
                string.Equals(operation, "publish", StringComparison.Ordinal) ? "topic" : "queue"));
        }
        _add(headers, MessagingHeaders.Network, "messaging.network.name", attributes);
        _add(headers, MessagingHeaders.SenderIdentity, "messaging.source.name", attributes);
        _add(headers, MessagingHeaders.MessageType, "messaging.message.type", attributes);
        return attributes.ToArray();
    }

    private static void _add(
        IReadOnlyDictionary<string, string> headers,
        string key,
        string attribute,
        ICollection<KeyValuePair<string, object?>> values)
    {
        if (headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            values.Add(new(attribute, value));
    }

    private static bool _tryGetSentTime(
        IReadOnlyDictionary<string, string> headers,
        out DateTimeOffset sentTime)
    {
        sentTime = default;
        return (headers.TryGetValue(MessagingHeaders.SentTime, out var value)
                || headers.TryGetValue(MessagingHeaders.RebusSentTime, out value))
            && DateTimeOffset.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out sentTime);
    }

    private static bool _isInstrumentationException(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException;
    }
}
