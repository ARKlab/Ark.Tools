// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.AspNetCore.OTel;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies that <see cref="Ex.AddArkAzureMonitorOpenTelemetry"/> keeps
/// <see cref="ArkAdaptiveSampler"/> as the effective sampler when Azure Monitor is enabled.
/// </summary>
[TestClass]
public sealed class AzureMonitorSamplerTests
{
    // Placeholder Azure Monitor target: the ingestion and live endpoints point to a closed local port.
    private const string _azureMonitorTarget =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;"
        + "IngestionEndpoint=http://127.0.0.1:1/;LiveEndpoint=http://127.0.0.1:1/";

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddArkAzureMonitorOpenTelemetry_EffectiveSamplerIsArkAdaptiveSampler(bool enableAzureMonitor)
    {
        using var serviceProvider = _buildServiceProvider(enableAzureMonitor ? _azureMonitorTarget : null);

        var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

        _getSampler(tracerProvider).Should().BeOfType<ArkAdaptiveSampler>();
    }

    [TestMethod]
    public void AddArkAzureMonitorOpenTelemetry_WithAzureMonitor_PromotesFailingChildOfRecordOnlyRoot()
    {
        var sourceName = nameof(AddArkAzureMonitorOpenTelemetry_WithAzureMonitor_PromotesFailingChildOfRecordOnlyRoot);
        using var collector = new CollectingProcessor();
        using var serviceProvider = _buildServiceProvider(
            _azureMonitorTarget,
            services => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing
                .AddSource(sourceName)
                .AddProcessor(collector)));
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(sourceName);

        var root = source.StartActivity("ROOT", ActivityKind.Server);
        root.Should().NotBeNull("an exhausted adaptive budget records the root instead of dropping it");
        root!.Recorded.Should().BeFalse("the root must be RecordOnly so failures can still promote it");

        var child = source.StartActivity("CHILD", ActivityKind.Internal, root.Context);
        child.Should().NotBeNull();
        child!.SetStatus(ActivityStatusCode.Error, "child failed");
        child.Stop();
        root.Stop();

        collector.Spans.Select(static a => a.DisplayName).Should().BeEquivalentTo(["CHILD", "ROOT"]);
    }

    [TestMethod]
    public void AddArkAzureMonitorOpenTelemetry_WithAzureMonitor_DropsPreFilteredSpans()
    {
        var sourceName = nameof(AddArkAzureMonitorOpenTelemetry_WithAzureMonitor_DropsPreFilteredSpans);
        using var serviceProvider = _buildServiceProvider(
            _azureMonitorTarget,
            services => services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(sourceName)));
        _ = serviceProvider.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(sourceName);

        using var activity = source.StartActivity(
            "OPTIONS",
            ActivityKind.Server,
            default(ActivityContext),
            [new(ArkAdaptiveSampler.FilteredTagName, true)]);

        (activity?.IsAllDataRequested ?? false).Should().BeFalse(
            "ArkAdaptiveSampler drops spans marked as filtered noise instead of recording them");
    }

    private static ServiceProvider _buildServiceProvider(
        string? azureMonitorTarget,
        Action<IServiceCollection>? configureServices = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ApplicationInsights:ConnectionString"] = azureMonitorTarget,
                ["ApplicationInsights:ArkAdaptiveSampler:TracesPerSecond"] = "0.0001",
                ["ApplicationInsights:ArkAdaptiveSampler:EnablePerOperationBucketing"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddArkAzureMonitorOpenTelemetry(configuration);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static Sampler? _getSampler(TracerProvider tracerProvider)
    {
        // The SDK does not expose the configured sampler publicly.
        return tracerProvider.GetType()
            .GetProperty("Sampler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetValue(tracerProvider) as Sampler;
    }
}
