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

using System.Diagnostics;

using Ark.Tools.AspNetCore.HealthChecks.OTel;
using Ark.Tools.OTel;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// OpenTelemetry setup extensions for Ark ASP.NET Core hosts.
/// </summary>
public static class Ex
{
    private const string _azureActivitySourceSwitch = "Azure.Experimental.EnableActivitySource";
    private const string _azureCoreHttpSourceName = "Azure.Core.Http";

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
    /// Adds the Ark ASP.NET Core OpenTelemetry setup with Azure SDK tracing, and Azure Monitor export when a
    /// connection string is configured. OpenTelemetry log export is restricted to errors and critical failures.
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

        // Registered before the Ark setup, whose canonical OTel:ArkAdaptiveSampler section then wins.
        if (configuration is not null)
        {
            services.Configure<ArkAdaptiveSamplerOptions>(options =>
                configuration.GetSection("ApplicationInsights:ArkAdaptiveSampler").Bind(options));
        }

        // Azure SDKs emit Service Bus and Event Hubs spans only with this experimental switch on. The package's
        // buildTransitive props set it in the runtime configuration; this covers project references.
        // An explicit application or environment choice wins.
        if (!AppContext.TryGetSwitch(_azureActivitySourceSwitch, out _)
            && Environment.GetEnvironmentVariable("AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE") is null)
        {
            AppContext.SetSwitch(_azureActivitySourceSwitch, true);
        }

        // Captured before UseAzureMonitor, which replaces the HttpClient filter.
        Func<HttpRequestMessage, bool>? applicationHttpFilter = null;
        services.Configure<HttpClientTraceInstrumentationOptions>(options =>
            applicationHttpFilter = options.FilterHttpRequestMessage);

        _addArkAspNetCoreOpenTelemetry(
                builder,
                configureSqlClient,
                configureArkOtel,
                configureAdaptiveSampler,
                configuration)
            .WithTracing(static tracing => tracing.AddSource("Azure.*"));

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.UseAzureMonitor(options => options.ConnectionString = connectionString);

            // UseAzureMonitor sets its own sampler; set Ark's again so the later registration wins.
            builder.WithTracing(static tracing =>
                tracing.SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>()));
        }

        // Azure SDKs already emit a client span for each HTTP call: drop the nested HttpClient span, as the Distro
        // does, and keep the application's filter.
        services.Configure<HttpClientTraceInstrumentationOptions>(options =>
        {
            var applicationFilter = applicationHttpFilter;
            options.FilterHttpRequestMessage = request =>
                Activity.Current?.Parent?.Source.Name != _azureCoreHttpSourceName
                && (applicationFilter is null || applicationFilter(request));
        });

        return services;
    }
}
