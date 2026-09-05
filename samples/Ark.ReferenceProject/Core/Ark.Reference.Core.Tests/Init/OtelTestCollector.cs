using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.Reference.Core.Tests.Init;

internal sealed class OtelTestCollector : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly System.Collections.Concurrent.ConcurrentQueue<OtelSpan> _spans = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<OtelMetric> _metrics = new();

    internal OtelTestCollector()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = static _ => true,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _spans.Enqueue(new OtelSpan(
                activity.Source.Name,
                activity.DisplayName,
                activity.TagObjects.ToDictionary(
                    static tag => tag.Key,
                    static tag => tag.Value?.ToString(),
                    StringComparer.Ordinal)))
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = static (instrument, listener) => listener.EnableMeasurementEvents(instrument)
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            _metrics.Enqueue(_metric(instrument, measurement, tags)));
        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            _metrics.Enqueue(_metric(instrument, measurement, tags)));
        _meterListener.Start();
    }

    internal IReadOnlyCollection<OtelSpan> _getSpans() => _spans.ToArray();
    internal IReadOnlyCollection<OtelMetric> _getMetrics() => _metrics.ToArray();

    internal void _reset()
    {
        while (_spans.TryDequeue(out var discardedSpan))
        {
            continue;
        }

        while (_metrics.TryDequeue(out var discardedMetric))
        {
            continue;
        }
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    private static OtelMetric _metric(
        Instrument instrument,
        object value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        return new OtelMetric(
            instrument.Meter.Name,
            instrument.Name,
            Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            tags.ToArray().ToDictionary(
                static tag => tag.Key,
                static tag => tag.Value?.ToString(),
                StringComparer.Ordinal));
    }
}

internal sealed record OtelSpan(
    string SourceName,
    string Name,
    IReadOnlyDictionary<string, string?> Tags);

internal sealed record OtelMetric(
    string MeterName,
    string Name,
    double Value,
    IReadOnlyDictionary<string, string?> Tags);
