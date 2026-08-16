// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

using Ark.Tools.Rebus;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// OpenTelemetry setup extensions for Ark ASP.NET Core hosts.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Adds the Azure Monitor OpenTelemetry Distro and Ark instrumentation sources.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkAzureMonitorOpenTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenTelemetry()
            .UseAzureMonitor()
            .WithTracing(tracing => tracing
                .AddSource(OpenTelemetryStep.ActivitySourceName)
                .AddSource("Ark.Tools.ResourceWatcher")
                .AddProcessor(new WebApi4xxAsSuccessProcessor()))
            .WithMetrics(metrics => metrics.AddMeter(OpenTelemetryProcessingMetricsStep.MeterName));

        return services;
    }

    private sealed class WebApi4xxAsSuccessProcessor : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.Kind != ActivityKind.Server)
                return;

            var statusCode = data.GetTagItem("http.response.status_code") switch
            {
                int value => value,
                long value => (int)value,
                string value when int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };

            if (statusCode is >= 400 and < 500)
                data.SetStatus(ActivityStatusCode.Unset);
        }
    }
}
