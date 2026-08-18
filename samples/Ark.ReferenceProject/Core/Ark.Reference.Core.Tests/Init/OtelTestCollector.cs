using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;

namespace Ark.Reference.Core.Tests.Init;

internal sealed class OtelTestCollector : IDisposable
{
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private readonly ConcurrentQueue<OtelSpan> _spans = new();
    private readonly ConcurrentQueue<OtelMetric> _metrics = new();

    internal OtelTestCollector()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _spans.Enqueue(new OtelSpan(
                activity.Source.Name,
                activity.DisplayName,
                activity.Tags.ToDictionary(
                    tag => tag.Key,
                    tag => tag.Value?.ToString(),
                    StringComparer.Ordinal)))
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) => listener.EnableMeasurementEvents(instrument)
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
            _metrics.Enqueue(_metric(instrument, measurement, tags)));
        _meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
            _metrics.Enqueue(_metric(instrument, measurement, tags)));
        _meterListener.Start();
    }

    internal IReadOnlyCollection<OtelSpan> Spans => _spans.ToArray();
    internal IReadOnlyCollection<OtelMetric> Metrics => _metrics.ToArray();

    internal void Reset()
    {
        while (_spans.TryDequeue(out _))
        {
        }

        while (_metrics.TryDequeue(out _))
        {
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
            Convert.ToDouble(value, CultureInfo.InvariantCulture),
            tags.ToArray().ToDictionary(
                tag => tag.Key,
                tag => tag.Value?.ToString(),
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
