using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Metrics;

using Rebus.Extensions;
using Rebus.Time;

using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Rebus;

/// <summary>
/// Collects Rebus queue and handler processing metrics.
/// </summary>
[StepDocumentation("ApplicationInsights Metric tracking: TimeInQueue (success-only) and ProcessingTime")]
public class ApplicationInsightsProcessingMetricsStep : IIncomingStep
{
    private readonly Container _container;
    private readonly IRebusTime _time;
    private readonly IProcessingMetrics _metrics;


    public ApplicationInsightsProcessingMetricsStep(Container container, IRebusTime time)
    {
        _container = container;
        _time = time;

        _metrics = new ApplicationInsightsMetrics(_container.GetInstance<TelemetryClient>());
    }

    internal ApplicationInsightsProcessingMetricsStep(IProcessingMetrics metrics, IRebusTime time)
    {
        _container = null!;
        _time = time;
        _metrics = metrics;
    }

    /// <inheritdoc/>
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var transportMessage = context.Load<TransportMessage>();

        var messageType = transportMessage.Headers.GetValueOrNull(Headers.Type);
        var sw = Stopwatch.StartNew();
        var operationResult = "failure";

        try
        {
            await next().ConfigureAwait(false);
            sw.Stop();
            var now = _time.Now;
            operationResult = "success";

            try
            {
                var enqueuedTime = DateTimeOffset.Parse(transportMessage.Headers[Headers.SentTime], CultureInfo.InvariantCulture);
                var totalTime = now - enqueuedTime;
                var timeInQueue = totalTime - sw.Elapsed;

                _metrics.TrackTimeInQueue(timeInQueue, messageType);
            }
#pragma warning disable ERP022
            catch
            {
                // Ignore telemetry errors so message processing is unaffected.
            }
#pragma warning restore ERP022
        }
        finally
        {
            try
            {
                _metrics.TrackMessageProcessing(TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds), messageType, operationResult);
            }
#pragma warning disable ERP022
            catch
            {
                // Ignore telemetry errors so message processing is unaffected.
            }
#pragma warning restore ERP022
        }

    }
    internal interface IProcessingMetrics
    {
        void TrackTimeInQueue(TimeSpan timeInQueue, string messageType);

        void TrackMessageProcessing(TimeSpan messageProcessing, string messageType, string operationResult);
    }

    private sealed class ApplicationInsightsMetrics : IProcessingMetrics
    {
        private readonly Metric _timeInQueue;
        private readonly Metric _messageProcessing;

        internal ApplicationInsightsMetrics(TelemetryClient client)
        {
            _timeInQueue = client.GetMetric(new MetricIdentifier("Rebus", "MessageTimeInQueueSuccess", "MessageType"));
            _messageProcessing = client.GetMetric(new MetricIdentifier("Rebus", "MessageProcessingTime", "MessageType", "OperationResult"));
        }

        public void TrackTimeInQueue(TimeSpan timeInQueue, string messageType)
        {
            _timeInQueue.TrackValue(_sanitize(timeInQueue), messageType);
        }

        public void TrackMessageProcessing(TimeSpan messageProcessing, string messageType, string operationResult)
        {
            _messageProcessing.TrackValue(_sanitize(messageProcessing), messageType, operationResult);
        }

        private static uint _sanitize(TimeSpan span)
        {
            var totalMilliseconds = span.TotalMilliseconds;
            if (totalMilliseconds < 0)
                return 0;

            if (totalMilliseconds > UInt32.MaxValue)
                return UInt32.MaxValue;

            return (uint)totalMilliseconds;
        }
    }
}