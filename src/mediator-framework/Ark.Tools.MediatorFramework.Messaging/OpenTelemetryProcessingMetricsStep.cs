// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime;

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

        await next().ConfigureAwait(false);
    }
}

/// <summary>Defines the stable OpenTelemetry messaging metric contract.</summary>
public static class MessagingMetrics
{
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
    internal const string ProcessingStartItem = "ark.messaging.metrics.processing-start";

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

    internal static void RecordClientOperation(
        TimeSpan duration,
        IReadOnlyDictionary<string, string> headers,
        string operation,
        string destination)
    {
        try
        {
            _clientOperationDuration.Record(
                Math.Max(0, duration.TotalSeconds),
                _attributes(headers, operation, destination));
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
        }
    }

    internal static void RecordProcessing(
        TimeSpan duration,
        IReadOnlyDictionary<string, string> headers,
        string outcome,
        int deliveryCount,
        string? destination = null)
    {
        try
        {
            var attributes = _attributes(headers, "process", destination)
                .Append(new KeyValuePair<string, object?>("messaging.process.result", outcome))
                .ToArray();
            _processDuration.Record(Math.Max(0, duration.TotalSeconds), attributes);
            _processedMessages.Add(1, attributes);
            if (deliveryCount > 0)
                _deliveryAttempts.Record(deliveryCount, attributes);
            if (_tryGetSentTime(headers, out var sentTime))
                _timeInQueue.Record(Math.Max(0, (DateTimeOffset.UtcNow - sentTime - duration).TotalSeconds), attributes);
        }
        catch (Exception exception) when (_isInstrumentationException(exception))
        {
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
        return headers.TryGetValue(MessagingHeaders.SentTime, out var value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out sentTime);
    }

    private static bool _isInstrumentationException(Exception exception)
    {
        return exception is not OutOfMemoryException
            and not StackOverflowException
            and not AccessViolationException;
    }
}
