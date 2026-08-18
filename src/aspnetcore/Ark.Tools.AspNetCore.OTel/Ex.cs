// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

using Ark.Tools.Rebus;
using Ark.Tools.OTel;

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

        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        return builder
            .ConfigureResource(resource => resource.AddArkTelemetryResource())
            .WithTracing(tracing => tracing
                .AddSource(OpenTelemetryStep.ActivitySourceName)
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddSource("Azure.Messaging.ServiceBus")
                .AddProcessor(new WebApi4xxAsSuccessProcessor()))
            .WithMetrics(metrics => metrics
                .AddMeter(OpenTelemetryProcessingMetricsStep.MeterName));
    }

    /// <summary>
    /// Adds Ark instrumentation sources and Azure Monitor when a connection string is configured.
    /// OpenTelemetry log export is restricted to errors and critical failures.
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
            ?? configuration?["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        if (!string.IsNullOrWhiteSpace(connectionString))
            builder.UseAzureMonitor(options => options.ConnectionString = connectionString);

        services.AddLogging(logging =>
            logging.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error));

        builder.AddArkAspNetCoreOpenTelemetry();

        return services;
    }
}
