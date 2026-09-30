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
    public async Task Publish_EmitsStatusAndDuration()
    {
        using var publisher = new ArkOTelHealthCheckPublisher();
        var values = new Dictionary<string, double>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = static (i, l) =>
        {
            if (i.Meter.Name == ArkOTelHealthCheckPublisher.MeterName) l.EnableMeasurementEvents(i);
        };
        listener.SetMeasurementEventCallback<double>((i, v, _, _) => values[i.Name] = v);
        listener.Start();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["db"] = new(HealthStatus.Degraded, null, TimeSpan.FromSeconds(2), null, null),
            },
            TimeSpan.FromSeconds(2));
        await publisher.PublishAsync(report, CancellationToken.None);
        listener.RecordObservableInstruments();

        values["health_check.status"].Should().Be(0.5);
        values["health_check.duration"].Should().Be(2);
    }
}
