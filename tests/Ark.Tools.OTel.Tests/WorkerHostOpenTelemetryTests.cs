// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.OTel;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Reflection;

namespace Ark.Tools.OTel.Tests;

/// <summary>
/// Verifies that <see cref="Ex.AddArkOpenTelemetryForWorkerHost"/> applies the Ark pipeline to worker hosts.
/// </summary>
[TestClass]
public sealed class WorkerHostOpenTelemetryTests
{
    [TestMethod]
    public void AddArkOpenTelemetryForWorkerHost_UsesArkSamplerAndResourceWatcherSource()
    {
        using var host = new HostBuilder().AddArkOpenTelemetryForWorkerHost().Build();

        var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
        using var source = new ActivitySource(ResourceWatcherInstrumentation.ActivitySourceName);

        // The SDK does not expose the configured sampler publicly.
        tracerProvider.GetType()
            .GetProperty("Sampler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .GetValue(tracerProvider)
            .Should().BeOfType<ArkAdaptiveSampler>();
        source.HasListeners().Should().BeTrue();
    }

    [TestMethod]
    public void AddArkOpenTelemetryForWorkerHost_BindsAdaptiveSamplerFromHostConfiguration()
    {
        using var host = new HostBuilder()
            .ConfigureHostConfiguration(static config => config.AddInMemoryCollection(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["OTel:ArkAdaptiveSampler:TracesPerSecond"] = "42",
                }))
            .AddArkOpenTelemetryForWorkerHost()
            .Build();

        host.Services.GetRequiredService<IOptions<ArkAdaptiveSamplerOptions>>().Value.TracesPerSecond
            .Should().Be(42);
    }
}
