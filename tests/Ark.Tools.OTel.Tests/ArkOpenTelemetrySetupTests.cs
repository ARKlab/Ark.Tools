// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies the sources and meters collected by the Ark OpenTelemetry setups.
/// </summary>
[TestClass]
// Asserts a source has no listeners: a parallel test's Azure.* listener would make it fail.
[DoNotParallelize]
public sealed class ArkOpenTelemetrySetupTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Setup_CollectsCommonSourcesAndMeters(bool aspNetCore)
    {
        using var exporter = new NoopMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var serviceProvider = _buildServiceProvider(aspNetCore, reader);

        // Outbox is collected by a literal name, without a reference to Ark.Tools.Outbox.
        _listens(OutboxProcessorBase.InstrumentationName).Should().BeTrue();
        _meterEnabled(OutboxProcessorBase.InstrumentationName).Should().BeTrue();
        _meterEnabled("System.Net.Http").Should().BeTrue();
        _meterEnabled("System.Runtime").Should().BeTrue();
        _listens("Azure.Messaging.ServiceBus").Should().BeFalse("Azure SDK sources belong to the Azure Monitor setup");
    }

    [TestMethod]
    public void AddArkAspNetCoreOpenTelemetry_CollectsServerSpansAndMetrics()
    {
        using var exporter = new NoopMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var serviceProvider = _buildServiceProvider(aspNetCore: true, reader);

        _listens("Microsoft.AspNetCore").Should().BeTrue();
        _meterEnabled("Microsoft.AspNetCore.Hosting").Should().BeTrue();
        AppContext.TryGetSwitch("Microsoft.AspNetCore.Hosting.SuppressActivityOpenTelemetryData", out var suppressed)
            .Should().BeTrue();
        suppressed.Should().BeFalse("ASP.NET Core passes the request tags at creation, as it does by default from version 11");
    }

    [TestMethod]
    public void Setup_DropsLowValueMetrics()
    {
        using var exporter = new NoopMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var serviceProvider = _buildServiceProvider(aspNetCore: false, reader);

        _instrumentEnabled("System.Net.Http", "http.client.open_connections").Should().BeFalse();
        _instrumentEnabled("System.Net.Http", "http.client.active_requests").Should().BeFalse();
        _instrumentEnabled("System.Runtime", "dotnet.assembly.count").Should().BeFalse();
        _instrumentEnabled("System.Runtime", "dotnet.jit.compiled_methods").Should().BeFalse("dotnet.jit.* is dropped");
        _instrumentEnabled("System.Net.Http", "http.client.request.duration").Should().BeTrue();
        _instrumentEnabled("System.Runtime", "dotnet.gc.collections").Should().BeTrue();
    }

    [TestMethod]
    public void Setup_CollectsMetricsRemovedFromDroppedMetrics()
    {
        using var exporter = new NoopMetricExporter();
        using var reader = new BaseExportingMetricReader(exporter);
        using var serviceProvider = _buildServiceProvider(
            aspNetCore: false,
            reader,
            static otel =>
            {
                otel.DroppedMetrics.Remove("dotnet.jit.*");
                otel.DroppedMetrics.Add("dotnet.gc.collections");
            });

        _instrumentEnabled("System.Runtime", "dotnet.jit.compiled_methods").Should().BeTrue();
        _instrumentEnabled("System.Runtime", "dotnet.gc.collections").Should().BeFalse();
        _instrumentEnabled("System.Runtime", "dotnet.assembly.count").Should().BeFalse();
    }

    private static ServiceProvider _buildServiceProvider(
        bool aspNetCore,
        MetricReader reader,
        Action<ArkOtelConfig>? configureArkOtel = null)
    {
        var services = new ServiceCollection();
        var builder = services.AddOpenTelemetry();
        if (aspNetCore)
            builder.AddArkAspNetCoreOpenTelemetry(configureArkOtel: configureArkOtel);
        else
            builder.AddArkOpenTelemetry(configureArkOtel: configureArkOtel);
        builder.WithMetrics(metrics => metrics.AddReader(reader));
        var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        _ = serviceProvider.GetRequiredService<MeterProvider>();
        return serviceProvider;
    }

    private static bool _listens(string sourceName)
    {
        using var source = new ActivitySource(sourceName);
        return source.HasListeners();
    }

    private static bool _meterEnabled(string meterName)
    {
        return _instrumentEnabled(meterName, "probe");
    }

    private static bool _instrumentEnabled(string meterName, string instrumentName)
    {
        using var meter = new Meter(meterName);
        return meter.CreateCounter<long>(instrumentName).Enabled;
    }
}
