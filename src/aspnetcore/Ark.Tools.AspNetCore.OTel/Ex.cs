// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry;
using OpenTelemetry.Trace;

using System.Diagnostics;

using Ark.Tools.Rebus;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// OpenTelemetry setup extensions for Ark ASP.NET Core hosts.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Adds Ark ASP.NET Core instrumentation to an exporter-agnostic OpenTelemetry builder.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAspNetCoreOpenTelemetry(this OpenTelemetryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithTracing(tracing => tracing
                .AddSource(OpenTelemetryStep.ActivitySourceName)
                .AddProcessor(new WebApi4xxAsSuccessProcessor()))
            .WithMetrics(metrics => metrics
                .AddMeter(OpenTelemetryProcessingMetricsStep.MeterName));
    }

    /// <summary>
    /// Adds the Azure Monitor OpenTelemetry Distro and Ark instrumentation sources.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">Optional application configuration.</param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkAzureMonitorOpenTelemetry(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOpenTelemetry();
        var connectionString = configuration?["ApplicationInsights:ConnectionString"]
            ?? configuration?["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
            builder.UseAzureMonitor();
        else
            builder.UseAzureMonitor(options => options.ConnectionString = connectionString);

        builder.AddArkAspNetCoreOpenTelemetry();

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
