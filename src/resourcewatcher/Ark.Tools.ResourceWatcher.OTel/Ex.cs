// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using Ark.Tools.ResourceWatcher;

namespace Ark.Tools.ResourceWatcher.OTel;

/// <summary>
/// OpenTelemetry setup extensions for ResourceWatcher hosts.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Enables ResourceWatcher activities in the host OpenTelemetry providers.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The original host builder.</returns>
    public static IHostBuilder AddArkOpenTelemetryForWorkerHost(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices(services =>
        {
            services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(ResourceWatcherDiagnosticSourceName))
                .WithMetrics(metrics => metrics.AddMeter(ResourceWatcherDiagnosticSourceName));
        });
    }

    private const string ResourceWatcherDiagnosticSourceName = "Ark.Tools.ResourceWatcher";
}
