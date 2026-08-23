// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;

using NodaTime;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Collects incoming message queue and processing metrics through OpenTelemetry.</summary>
public sealed class OpenTelemetryProcessingMetricsStep : IMessagingIncomingStep
{
    /// <summary>The meter used by MediatorFramework messaging instrumentation.</summary>
    public const string MeterName = OpenTelemetryIncomingStep.ActivitySourceName;

    private const string _messageTimeInQueueName = "ark.tools.mediatorframework.message_time_in_queue_success";
    private const string _messageProcessingTimeName = "ark.tools.mediatorframework.message_processing_time";
    private const string _operationSuccess = "success";
    private const string _operationFailure = "failure";

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

        var messageType = _getHeader(
            context.Headers,
            MessagingHeaders.MessageType,
            MessagingHeaders.RebusType) ?? "unknown";
        var stopwatch = Stopwatch.StartNew();
        var operationResult = _operationFailure;

        try
        {
            await next().ConfigureAwait(false);
            stopwatch.Stop();
            operationResult = _operationSuccess;

            try
            {
                if (_tryGetSentTime(context.Headers, out var sentTime))
                {
                    var timeInQueue = _clock.GetCurrentInstant().ToDateTimeOffset() - sentTime - stopwatch.Elapsed;
                    Metrics._trackTimeInQueue(timeInQueue, messageType);
                }
            }
#pragma warning disable ERP022
            catch
            {
            }
#pragma warning restore ERP022
        }
        finally
        {
            try
            {
                Metrics._trackMessageProcessing(stopwatch.Elapsed, messageType, operationResult);
            }
#pragma warning disable ERP022
            catch
            {
            }
#pragma warning restore ERP022
        }
    }

    private static bool _tryGetSentTime(
        IReadOnlyDictionary<string, string> headers,
        out DateTimeOffset sentTime)
    {
        var value = _getHeader(headers, MessagingHeaders.SentTime, MessagingHeaders.RebusSentTime);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out sentTime);
    }

    private static string? _getHeader(
        IReadOnlyDictionary<string, string> headers,
        string key,
        string fallbackKey)
    {
        return headers.TryGetValue(key, out var value)
            ? value
            : headers.TryGetValue(fallbackKey, out value)
                ? value
                : null;
    }

    private static class Metrics
    {
        private static readonly Meter _meter = new(MeterName);
        private static readonly Histogram<double> _timeInQueue =
            _meter.CreateHistogram<double>(_messageTimeInQueueName, "ms");
        private static readonly Histogram<double> _messageProcessing =
            _meter.CreateHistogram<double>(_messageProcessingTimeName, "ms");

        internal static void _trackTimeInQueue(TimeSpan timeInQueue, string messageType)
        {
            _timeInQueue.Record(
                _sanitize(timeInQueue),
                new KeyValuePair<string, object?>("message.type", messageType));
        }

        internal static void _trackMessageProcessing(
            TimeSpan messageProcessing,
            string messageType,
            string operationResult)
        {
            _messageProcessing.Record(
                _sanitize(messageProcessing),
                new KeyValuePair<string, object?>("message.type", messageType),
                new KeyValuePair<string, object?>("operation.result", operationResult));
        }

        private static double _sanitize(TimeSpan span)
        {
            return Math.Max(0, span.TotalMilliseconds);
        }
    }
}
