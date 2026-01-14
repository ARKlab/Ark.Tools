using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Metrics;

using Rebus.Extensions;
using Rebus.Time;

using SimpleInjector;

using System.Diagnostics;

namespace Ark.Tools.Rebus;

[StepDocumentation("ApplicationInsights Metric tracking: TimeInQueue (success-only) and ProcessingTime")]
public class ApplicationInsightsProcessingMetricsStep : IIncomingStep
{
    private readonly Container _container;
    private readonly IRebusTime _time;
    private readonly Lazy<Metrics> _metrics;


    public ApplicationInsightsProcessingMetricsStep(Container container, IRebusTime time)
    {
        _container = container;
        _time = time;

        _metrics = new Lazy<Metrics>(() => new Metrics(_container.GetInstance<TelemetryClient>()), System.Threading.LazyThreadSafetyMode.PublicationOnly);
    }


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

            var enqueuedTime = DateTimeOffset.Parse(MessageContext.Current.Headers[Headers.SentTime], CultureInfo.InvariantCulture);
            var totalTime = now - enqueuedTime;
            var timeInQueue = totalTime - TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds);

            _metrics.Value.TrackTimeInQueue(timeInQueue, messageType);
        }
        finally
        {
            _metrics.Value.TrackMessageProcessing(TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds), messageType, operationResult);
        }

    }
    sealed class Metrics
    {
        private static readonly MetricConfigurationForMeasurement _defaultConfigForMeasurement = new(
                                                                10000,
                                                                10000,
                                                                new MetricSeriesConfigurationForMeasurement(restrictToUInt32Values: true));
        private readonly Metric _timeInQueue;
        private readonly Metric _messageProcessing;

        internal Metrics(TelemetryClient client)
        {
            _timeInQueue = client.GetMetric(new MetricIdentifier("Rebus", "Message TimeInQueue (Success)", "MessageType"), _defaultConfigForMeasurement);
            _messageProcessing = client.GetMetric(new MetricIdentifier("Rebus", "Message ProcessingTime", "MessageType", "OperationResult"), _defaultConfigForMeasurement);
        }

        internal void TrackTimeInQueue(TimeSpan timeInQueue, string messageType)
        {
            _timeInQueue.TrackValue(_sanitize(timeInQueue), messageType);
        }

        internal void TrackMessageProcessing(TimeSpan messageProcessing, string messageType, string operationResult)
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