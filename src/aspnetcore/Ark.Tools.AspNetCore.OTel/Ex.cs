// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.Http;
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
    private const string _azureActivitySourceSwitch = "Azure.Experimental.EnableActivitySource";

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
    /// Optional application configuration for the adaptive sampler.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAspNetCoreOpenTelemetry(
        this OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        Action<ArkOtelConfig>? configureArkOtel = null,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return _addArkAspNetCoreOpenTelemetry(
            builder,
            configureSqlClient,
            configureArkOtel,
            configureAdaptiveSampler,
            configuration: null);
    }

    private static OpenTelemetryBuilder _addArkAspNetCoreOpenTelemetry(
        OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient,
        Action<ArkOtelConfig>? configureArkOtel,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler,
        IConfiguration? configuration)
    {
        builder.Services.AddArkOTelHealthCheckPublisher();

        return builder
            .AddArkOpenTelemetry(configureSqlClient, configureArkOtel, configureAdaptiveSampler, configuration)
            .WithTracing(static tracing => tracing.AddAspNetCoreInstrumentation())
            .WithMetrics(static metrics => metrics
                // Native ASP.NET Core request metrics, as the Azure Monitor Distro collects them.
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter(ArkOTelHealthCheckPublisher.MeterName));
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

        _addArkAspNetCoreOpenTelemetry(
            builder,
            configureSqlClient,
            configureArkOtel,
            configureAdaptiveSampler,
            configuration);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // The Distro collects Azure SDK spans (Service Bus included) only with this experimental switch on;
            // an explicit application or environment choice wins.
            if (!AppContext.TryGetSwitch(_azureActivitySourceSwitch, out _)
                && Environment.GetEnvironmentVariable("AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE") is null)
            {
                AppContext.SetSwitch(_azureActivitySourceSwitch, true);
            }

            // UseAzureMonitor replaces the HttpClient filter with its Azure SDK deduplication; keep the application's too.
            Func<HttpRequestMessage, bool>? applicationHttpFilter = null;
            services.Configure<HttpClientTraceInstrumentationOptions>(options =>
                applicationHttpFilter = options.FilterHttpRequestMessage);

            builder.UseAzureMonitor(options => options.ConnectionString = connectionString);

            services.Configure<HttpClientTraceInstrumentationOptions>(options =>
            {
                var distroFilter = options.FilterHttpRequestMessage;
                var applicationFilter = applicationHttpFilter;
                options.FilterHttpRequestMessage = request =>
                    (distroFilter is null || distroFilter(request))
                    && (applicationFilter is null || applicationFilter(request));
            });

            // UseAzureMonitor sets its own sampler; set Ark's again so the later registration wins.
            builder.WithTracing(static tracing =>
                tracing.SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>()));
        }

        return services;
    }
}
