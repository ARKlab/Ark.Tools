// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.AspNetCore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <summary>
    /// Adds Ark ASP.NET Core instrumentation to an exporter-agnostic OpenTelemetry builder.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder.</param>
    /// <param name="configureSqlClient">
    /// Optional application configuration applied after Ark SQL defaults.
    /// </param>
    /// <param name="includeSqlQueryText">
    /// Whether to retain SQL query text on exported spans. Defaults to <see langword="false"/>.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAspNetCoreOpenTelemetry(
        this OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        bool includeSqlQueryText = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .ConfigureResource(resource => resource.AddArkTelemetryResource())
            .WithTracing(tracing => tracing
                .AddSource(OpenTelemetryStep.ActivitySourceName)
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = _includeSqlClientSpan;
                    configureSqlClient?.Invoke(options);
                    var applicationFilter = options.Filter;
                    options.Filter = command =>
                        _includeSqlClientSpan(command)
                        && (applicationFilter is null || applicationFilter(command));
                })
                .AddSource("Azure.Messaging.ServiceBus")
                .AddProcessor(new ArkSqlClientSpanProcessor(includeSqlQueryText))
                .AddProcessor(new WebApi4xxAsSuccessProcessor()))
            .WithMetrics(metrics => metrics
                .AddMeter(OpenTelemetryProcessingMetricsStep.MeterName)
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
    /// <param name="includeSqlQueryText">
    /// Whether to retain SQL query text on exported spans. Defaults to <see langword="false"/>.
    /// </param>
    /// <returns>The original service collection.</returns>
    public static IServiceCollection AddArkAzureMonitorOpenTelemetry(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        bool includeSqlQueryText = false)
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

        builder.AddArkAspNetCoreOpenTelemetry(configureSqlClient, includeSqlQueryText);

        return services;
    }

    private static bool _includeSqlClientSpan(object command)
    {
        if (command is not DbCommand dbCommand)
            return true;

        var text = dbCommand.CommandText;
        return !text.Contains("READPAST", StringComparison.OrdinalIgnoreCase)
            || !text.Contains("ROWLOCK", StringComparison.OrdinalIgnoreCase)
            || !text.Contains("READCOMMITTEDLOCK", StringComparison.OrdinalIgnoreCase)
            || !text.Contains("DELETE FROM batch", StringComparison.OrdinalIgnoreCase);
    }
}
