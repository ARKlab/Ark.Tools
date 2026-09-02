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
