// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Functions.Hosting;
using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using System.Diagnostics.Metrics;

namespace Ark.MediatorFramework.Sample.Core.Functions.Tests;

/// <summary>Verifies the telemetry every Functions app registers.</summary>
[TestClass]
public sealed class FunctionsTelemetryTests
{
    private const string _placeholderSetting =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:9/";

    [TestMethod]
    [DataRow(null)]
    [DataRow(_placeholderSetting)]
    public void AddTelemetry_CollectsMessagingMetricsAndExportsOnlyWithAConnectionString(string? setting)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = setting,
            })
            .Build();

        FunctionsHosting.AddTelemetry(services, configuration);
        services.AddOpenTelemetry()
            .WithMetrics(static metrics => metrics.AddReader(new BaseExportingMetricReader(new NoopMetricExporter())));
        using var serviceProvider = services.BuildServiceProvider();
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        _ = serviceProvider.GetRequiredService<MeterProvider>();

        using var meter = new Meter(OpenTelemetryProcessingMetricsStep.MeterName);
        meter.CreateCounter<long>("probe").Enabled.Should().BeTrue();
        serviceProvider.GetRequiredService<IOptionsMonitor<AzureMonitorExporterOptions>>()
            .Get(Options.DefaultName).ConnectionString.Should().Be(setting);
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
