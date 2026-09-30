// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ark.Tools.AspNetCore.HealthChecks.OTel;

/// <summary>
/// Publishes health check reports as OpenTelemetry metrics.
/// </summary>
/// <remarks>
/// <c>health_check.status</c> is 1 (Healthy), 0.5 (Degraded) or 0 (Unhealthy) per check;
/// <c>health_check.duration</c> (seconds) is recorded per check on every publish.
/// </remarks>
public sealed class ArkOTelHealthCheckPublisher : IHealthCheckPublisher, IDisposable
{
    /// <summary>The meter name to register with OpenTelemetry.</summary>
    public const string MeterName = "Ark.HealthChecks";

    private readonly Meter _meter = new(MeterName);
    private readonly Histogram<double> _duration;
    private volatile HealthReport? _last;

    /// <summary>Creates the publisher and its instruments.</summary>
    public ArkOTelHealthCheckPublisher()
    {
        _duration = _meter.CreateHistogram<double>("health_check.duration", "s", "Health check duration");
        _meter.CreateObservableGauge(
            "health_check.status",
            _observeStatus,
            description: "Health check status: 1 Healthy, 0.5 Degraded, 0 Unhealthy");
    }

    /// <inheritdoc />
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);

        _last = report;
        foreach (var (name, entry) in report.Entries)
            _duration.Record(entry.Duration.TotalSeconds, new KeyValuePair<string, object?>("health_check.name", name));

        return Task.CompletedTask;
    }

    private IEnumerable<Measurement<double>> _observeStatus()
    {
        var report = _last;
        if (report is null)
            yield break;

        foreach (var (name, entry) in report.Entries)
        {
            var value = entry.Status switch
            {
                HealthStatus.Healthy => 1d,
                HealthStatus.Degraded => 0.5d,
                _ => 0d,
            };
            yield return new Measurement<double>(value, new KeyValuePair<string, object?>("health_check.name", name));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}
