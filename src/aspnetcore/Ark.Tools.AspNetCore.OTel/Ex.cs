// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using System.Data.Common;

using Ark.Tools.Rebus;
using Ark.Tools.OTel;

namespace Ark.Tools.AspNetCore.OTel;

/// <summary>
/// OpenTelemetry setup extensions for Ark ASP.NET Core hosts.
/// </summary>
public static class Ex
{
    private const string _mediatorMessagingInstrumentationName = "Ark.MediatorFramework.Messaging";

    /// <summary>
    /// Adds Ark ASP.NET Core instrumentation to an exporter-agnostic OpenTelemetry builder.
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
        var arkOtelConfig = new ArkOtelConfig();
        configureArkOtel?.Invoke(arkOtelConfig);
        var skippedSqlQueryLabels = _getSkippedSqlQueryLabels(arkOtelConfig.SqlQueryLabelsToSkip);
        var failedTraceRegistry = new FailedTraceRegistry();

        return builder
            .ConfigureResource(resource => resource.AddArkTelemetryResource())
            .WithTracing(tracing => tracing
                .ConfigureServices(services =>
                    _configureAdaptiveSampler(services, configuration, configureAdaptiveSampler))
                .AddProcessor(new ArkPreFilterProcessor())
                .SetSampler(services => new ArkAdaptiveSampler(
                    services.GetRequiredService<IOptions<ArkAdaptiveSamplerOptions>>().Value,
                    failedTraceRegistry))
                .AddSource(OpenTelemetryStep.ActivitySourceName)
                .AddSource(_mediatorMessagingInstrumentationName)
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    configureSqlClient?.Invoke(options);
                    var applicationFilter = options.Filter;
                    options.Filter = command =>
                        _includeSqlClientSpan(command, skippedSqlQueryLabels)
                        && (applicationFilter is null || applicationFilter(command));
                    var applicationEnricher = options.EnrichWithSqlCommand;
                    options.EnrichWithSqlCommand = (activity, command) =>
                    {
                        applicationEnricher?.Invoke(activity, command);
                        if (command is DbCommand dbCommand)
                            ArkSqlQueryLabel.SetTag(activity, dbCommand.CommandText);
                    };
                })
                .AddSource("Azure.Messaging.ServiceBus")
                .AddProcessor(new ArkSqlClientSpanProcessor(arkOtelConfig.IncludeSqlQueryText))
                .AddProcessor(new ArkFailurePromotionProcessor(failedTraceRegistry))
                .AddProcessor(new WebApi4xxAsSuccessProcessor()))
            .WithMetrics(metrics => metrics
                .AddMeter(OpenTelemetryProcessingMetricsStep.MeterName)
                .AddMeter(_mediatorMessagingInstrumentationName)
                .AddSqlClientInstrumentation());
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

        if (!string.IsNullOrWhiteSpace(connectionString))
            builder.UseAzureMonitor(options => options.ConnectionString = connectionString);

        services.AddLogging(logging =>
            logging.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error));

        _addArkAspNetCoreOpenTelemetry(
            builder,
            configureSqlClient,
            configureArkOtel,
            configureAdaptiveSampler,
            configuration);

        return services;
    }

    private static void _configureAdaptiveSampler(
        IServiceCollection services,
        IConfiguration? configuration,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler)
    {
        services.Configure<ArkAdaptiveSamplerOptions>(options =>
        {
            configuration?.GetSection("ApplicationInsights:ArkAdaptiveSampler").Bind(options);
            configureAdaptiveSampler?.Invoke(options);
        });
    }

    private static bool _includeSqlClientSpan(
        object command,
        IReadOnlySet<string> skippedSqlQueryLabels)
    {
        if (command is not DbCommand dbCommand)
            return true;

        var label = ArkSqlQueryLabel.Extract(dbCommand.CommandText);
        return label is null || !skippedSqlQueryLabels.Contains(label);
    }

    private static IReadOnlySet<string> _getSkippedSqlQueryLabels(
        IEnumerable<string>? sqlQueryLabelsToSkip)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "outbox.peek-lock"
        };

        if (sqlQueryLabelsToSkip is null)
            return labels;

        foreach (var label in sqlQueryLabelsToSkip)
        {
            if (!string.IsNullOrWhiteSpace(label))
                labels.Add(label.Trim());
        }

        return labels;
    }
}
