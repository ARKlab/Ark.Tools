// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Rebus.Extensions;
using Rebus.Time;

using SimpleInjector;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.Tools.Rebus;

/// <summary>
/// Collects Rebus queue and handler processing metrics through OpenTelemetry.
/// </summary>
[StepDocumentation("OpenTelemetry metric tracking: queue time (success-only) and processing time")]
public sealed class OpenTelemetryProcessingMetricsStep : IIncomingStep
{
    /// <summary>
    /// The meter used by Rebus instrumentation.
    /// </summary>
    public const string MeterName = "Ark.Tools.Rebus";

    private readonly IRebusTime _time;
    private readonly IProcessingMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of <see cref="OpenTelemetryProcessingMetricsStep"/>.
    /// </summary>
    /// <param name="container">The application SimpleInjector container.</param>
    /// <param name="time">The Rebus clock.</param>
    public OpenTelemetryProcessingMetricsStep(Container container, IRebusTime time)
    {
        ArgumentNullException.ThrowIfNull(container);
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _metrics = new OpenTelemetryMetrics();
    }

    internal OpenTelemetryProcessingMetricsStep(IProcessingMetrics metrics, IRebusTime time)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <inheritdoc/>
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var transportMessage = context.Load<TransportMessage>();
        var messageType = transportMessage.Headers.GetValueOrNull(Headers.Type) ?? "unknown";
        var stopwatch = Stopwatch.StartNew();
        var operationResult = "failure";

        try
        {
            await next().ConfigureAwait(false);
            stopwatch.Stop();
            operationResult = "success";

            try
            {
                var enqueuedTime = DateTimeOffset.Parse(
                    transportMessage.Headers[Headers.SentTime],
                    CultureInfo.InvariantCulture);
                var timeInQueue = _time.Now - enqueuedTime - stopwatch.Elapsed;
                _metrics.TrackTimeInQueue(timeInQueue, messageType);
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
                _metrics.TrackMessageProcessing(stopwatch.Elapsed, messageType, operationResult);
            }
#pragma warning disable ERP022
            catch
            {
            }
#pragma warning restore ERP022
        }
    }

    internal interface IProcessingMetrics
    {
        void TrackTimeInQueue(TimeSpan timeInQueue, string messageType);

        void TrackMessageProcessing(TimeSpan messageProcessing, string messageType, string operationResult);
    }

    private sealed class OpenTelemetryMetrics : IProcessingMetrics
    {
        private readonly Meter _meter = new(MeterName);
        private readonly Histogram<double> _timeInQueue;
        private readonly Histogram<double> _messageProcessing;

        public OpenTelemetryMetrics()
        {
            _timeInQueue = _meter.CreateHistogram<double>("Rebus.MessageTimeInQueueSuccess", "ms");
            _messageProcessing = _meter.CreateHistogram<double>("Rebus.MessageProcessingTime", "ms");
        }

        public void TrackTimeInQueue(TimeSpan timeInQueue, string messageType)
        {
            _timeInQueue.Record(_sanitize(timeInQueue), new KeyValuePair<string, object?>("MessageType", messageType));
        }

        public void TrackMessageProcessing(TimeSpan messageProcessing, string messageType, string operationResult)
        {
            _messageProcessing.Record(
                _sanitize(messageProcessing),
                new KeyValuePair<string, object?>("MessageType", messageType),
                new KeyValuePair<string, object?>("OperationResult", operationResult));
        }

        private static double _sanitize(TimeSpan span)
        {
            return Math.Max(0, span.TotalMilliseconds);
        }
    }
}
