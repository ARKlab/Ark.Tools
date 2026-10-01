// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.HealthChecks.OTel;

using AwesomeAssertions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

using System.Diagnostics.Metrics;

namespace Ark.Tools.OTel.Tests;

[TestClass]
public sealed class HealthCheckPublisherTests
{
    [TestMethod]
    [DataRow(HealthStatus.Healthy, 1d)]
    [DataRow(HealthStatus.Degraded, 0.5d)]
    [DataRow(HealthStatus.Unhealthy, 0d)]
    public async Task Publish_EmitsStatusAndDuration(HealthStatus status, double expected)
    {
        using var publisher = new ArkOTelHealthCheckPublisher();
        // the meter name is process-wide and tests run in parallel: isolate by a unique check name
        var checkName = "db-" + status;
        var values = new Dictionary<string, (double Value, object? Name)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = static (i, l) =>
        {
            if (i.Meter.Name == ArkOTelHealthCheckPublisher.MeterName) l.EnableMeasurementEvents(i);
        };
        listener.SetMeasurementEventCallback<double>((i, v, tags, _) =>
        {
            object? name = null;
            foreach (var t in tags)
                if (t.Key == "health_check.name") name = t.Value;
            if (Equals(name, checkName)) values[i.Name] = (v, name);
        });
        listener.Start();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                [checkName] = new(status, null, TimeSpan.FromSeconds(2), null, null),
            },
            TimeSpan.FromSeconds(2));
        await publisher.PublishAsync(report, CancellationToken.None);
        listener.RecordObservableInstruments();

        values["health_check.status"].Value.Should().Be(expected);
        values["health_check.status"].Name.Should().Be(checkName);
        values["health_check.duration"].Value.Should().Be(2);
        values["health_check.duration"].Name.Should().Be(checkName);
    }
}
