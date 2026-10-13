// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

using Ark.Tools.AspNetCore.HealthChecks.OTel;
using Ark.Tools.OTel;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// OpenTelemetry setup extensions for Ark ASP.NET Core hosts.
/// </summary>
public static class Ex
{
    /// <summary>
    /// Adds the Ark OpenTelemetry pipeline (see <see cref="ArkOpenTelemetryBuilderExtensions.AddArkOpenTelemetry"/>)
    /// and the health check metrics to an exporter-agnostic OpenTelemetry builder.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <param name="configureSqlClient">
    /// Optional application configuration applied after Ark SQL defaults.
    /// </param>
    /// <param name="configureArkOtel">
    /// Optional application configuration for Ark OpenTelemetry instrumentation.
    /// </param>
    /// <param name="configureAdaptiveSampler">
    /// Optional application configuration for the adaptive sampler, applied after <paramref name="configuration"/>.
    /// </param>
    /// <param name="configuration">
    /// Optional application configuration; the adaptive sampler binds the <c>OTel:ArkAdaptiveSampler</c>
    /// section over the legacy <c>ApplicationInsights:ArkAdaptiveSampler</c> section.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAspNetCoreOpenTelemetry(
        this OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        Action<ArkOtelConfig>? configureArkOtel = null,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler = null,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddArkOTelHealthCheckPublisher();

        return builder
            .AddArkOpenTelemetry(configureSqlClient, configureArkOtel, configureAdaptiveSampler, configuration)
            .WithMetrics(static metrics => metrics.AddMeter(ArkOTelHealthCheckPublisher.MeterName));
    }

    /// <summary>
    /// Adds Ark instrumentation sources and Azure Monitor when a connection string is configured.
    /// OpenTelemetry log export is restricted to errors and critical failures.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">Optional application configuration.</param>
    /// <param name="configureSqlClient">
    /// Optional application configuration applied after Ark SQL defaults.
    /// </param>
    /// <param name="configureArkOtel">
    /// Optional application configuration for Ark OpenTelemetry instrumentation.
    /// </param>
    /// <param name="configureAdaptiveSampler">
    /// Optional application configuration for the adaptive sampler.
    /// </param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkAzureMonitorOpenTelemetry(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        Action<ArkOtelConfig>? configureArkOtel = null,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = services.AddOpenTelemetry();
        var connectionString = configuration?["ApplicationInsights:ConnectionString"]
            ?? configuration?["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

        services.AddLogging(static logging =>
            logging.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error));

        builder.AddArkAspNetCoreOpenTelemetry(configureSqlClient, configureArkOtel, configureAdaptiveSampler, configuration);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.UseAzureMonitor(options => options.ConnectionString = connectionString);

            // UseAzureMonitor sets its own sampler; set Ark's again so the later registration wins.
            builder.WithTracing(static tracing =>
                tracing.SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>()));
        }

        return services;
    }
}
