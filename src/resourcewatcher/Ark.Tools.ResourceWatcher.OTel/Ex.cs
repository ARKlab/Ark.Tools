// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;

using Ark.Tools.OTel;

namespace Ark.Tools.ResourceWatcher.OTel;

/// <summary>
/// OpenTelemetry setup extensions for ResourceWatcher hosts.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Adds ResourceWatcher tracing and metrics to an exporter-agnostic OpenTelemetry builder.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkResourceWatcherOpenTelemetry(this OpenTelemetryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .ConfigureResource(static resource => resource.AddArkTelemetryResource())
            .WithTracing(static tracing => tracing
                .AddArkComplianceRedaction()
                .AddSource(ResourceWatcherInstrumentation.ActivitySourceName))
            .WithMetrics(static metrics => metrics.AddMeter(ResourceWatcherInstrumentation.MeterName));
    }

    /// <summary>
    /// Adds the Ark OpenTelemetry pipeline (see <see cref="ArkOpenTelemetryBuilderExtensions.AddArkOpenTelemetry"/>)
    /// and the ResourceWatcher activities and metrics to the host OpenTelemetry providers.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The original host builder.</returns>
    public static IHostBuilder AddArkOpenTelemetryForWorkerHost(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigureServices(static (context, services) =>
        {
            // AddArkOpenTelemetry already adds the resource and the redaction processor.
            services.AddOpenTelemetry()
                .AddArkOpenTelemetry(configuration: context.Configuration)
                .WithTracing(static tracing => tracing.AddSource(ResourceWatcherInstrumentation.ActivitySourceName))
                .WithMetrics(static metrics => metrics.AddMeter(ResourceWatcherInstrumentation.MeterName));
        });
    }
}
