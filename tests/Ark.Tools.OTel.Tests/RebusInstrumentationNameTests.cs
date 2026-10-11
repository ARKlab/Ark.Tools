// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Guards the Rebus source and meter names that <see cref="Ex.AddArkAspNetCoreOpenTelemetry"/> collects as literals,
/// without a reference to <c>Ark.Tools.Rebus</c>.
/// </summary>
[TestClass]
public sealed class RebusInstrumentationNameTests
{
    [TestMethod]
    public void AddArkAspNetCoreOpenTelemetry_CollectsTheRebusSourceAndMeter()
    {
        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .AddArkAspNetCoreOpenTelemetry()
            .WithMetrics(static metrics => metrics.AddReader(new BaseExportingMetricReader(new NoopMetricExporter())));
        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        _ = serviceProvider.GetRequiredService<MeterProvider>();

        using var source = new ActivitySource(Rebus.OpenTelemetryStep.ActivitySourceName);
        using var meter = new Meter(Rebus.OpenTelemetryProcessingMetricsStep.MeterName);
        var probe = meter.CreateCounter<long>("probe");

        source.HasListeners().Should().BeTrue();
        probe.Enabled.Should().BeTrue();
    }

    // Metric instruments are enabled only while a reader is attached.
    private sealed class NoopMetricExporter : BaseExporter<Metric>
    {
        public override ExportResult Export(in Batch<Metric> batch)
        {
            return ExportResult.Success;
        }
    }
}
