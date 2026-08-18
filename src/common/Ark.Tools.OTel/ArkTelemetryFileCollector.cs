// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace Ark.Tools.OTel;

/// <summary>
/// Collects all process-local OpenTelemetry activities and measurements as JSON Lines files.
/// </summary>
public sealed class ArkTelemetryFileCollector : IDisposable
{
    /// <summary>
    /// The environment variable that enables file collection.
    /// </summary>
    public const string DirectoryEnvironmentVariable = "ARK_OTEL_FILE_DIRECTORY";

#pragma warning disable MA0158 // object lock is required for net8.0 compatibility
    private readonly object _gate = new();
#pragma warning restore MA0158
    private readonly StreamWriter _spans;
    private readonly StreamWriter _metrics;
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArkTelemetryFileCollector"/> class.
    /// </summary>
    /// <param name="directory">The directory where the JSON Lines files are written.</param>
    public ArkTelemetryFileCollector(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);
        _spans = _createWriter(Path.Combine(directory, "otel-spans.jsonl"));
        _metrics = _createWriter(Path.Combine(directory, "otel-metrics.jsonl"));

        _activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _writeActivity
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) => listener.EnableMeasurementEvents(instrument)
        };
        _meterListener.SetMeasurementEventCallback<long>(_writeLongMeasurement);
        _meterListener.SetMeasurementEventCallback<double>(_writeDoubleMeasurement);
        _meterListener.Start();
    }

    /// <summary>
    /// Starts a collector when <see cref="DirectoryEnvironmentVariable"/> is configured.
    /// </summary>
    /// <returns>A collector, or <see langword="null"/> when file collection is disabled.</returns>
    public static ArkTelemetryFileCollector? StartFromEnvironment()
    {
        var directory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(directory) ? null : new ArkTelemetryFileCollector(directory);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _meterListener.Dispose();
        _activityListener.Dispose();
        lock (_gate)
        {
            _spans.Dispose();
            _metrics.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    private static StreamWriter _createWriter(string path)
    {
        return new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    private void _writeActivity(Activity activity)
    {
        _write(
            _spans,
            new
            {
                signal = "span",
                timestamp = activity.StartTimeUtc,
                duration_ms = activity.Duration.TotalMilliseconds,
                source = activity.Source.Name,
                name = activity.DisplayName,
                kind = activity.Kind.ToString(),
                trace_id = activity.TraceId.ToString(),
                span_id = activity.SpanId.ToString(),
                parent_span_id = activity.ParentSpanId.ToString(),
                status = activity.Status.ToString(),
                status_description = activity.StatusDescription,
                tags = _tags(activity.Tags.Select(tag =>
                    new KeyValuePair<string, object?>(tag.Key, tag.Value))),
                events = activity.Events.Select(activityEvent => new
                {
                    name = activityEvent.Name,
                    timestamp = activityEvent.Timestamp,
                    tags = _tags(activityEvent.Tags)
                })
            });
    }

    private void _writeLongMeasurement(
        Instrument instrument,
        long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        _writeMeasurement(instrument, measurement, tags);
    }

    private void _writeDoubleMeasurement(
        Instrument instrument,
        double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
    {
        _writeMeasurement(instrument, measurement, tags);
    }

    private void _writeMeasurement(
        Instrument instrument,
        object measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        _write(
            _metrics,
            new
            {
                signal = "metric",
                timestamp = DateTimeOffset.UtcNow,
                meter = instrument.Meter.Name,
                name = instrument.Name,
                unit = instrument.Unit,
                type = instrument.GetType().Name,
                value = measurement,
                tags = _tags(tags.ToArray())
            });
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The anonymous payload is created in this method and only contains JSON scalar values and collections.")]
    private void _write(StreamWriter writer, object value)
    {
        lock (_gate)
        {
            if (!_disposed)
                writer.WriteLine(JsonSerializer.Serialize(value));
        }
    }

    private static Dictionary<string, object?> _tags(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        return tags.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
    }
}
