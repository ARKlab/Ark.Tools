// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.SqlClient;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

using System.Data.Common;
using System.Diagnostics;

namespace Ark.Tools.OTel;

/// <summary>
/// Host-agnostic OpenTelemetry setup for Ark applications.
/// </summary>
public static class ArkOpenTelemetryBuilderExtensions
{
    private const string _mediatorMessagingInstrumentationName = "Ark.MediatorFramework.Messaging";
    // Ark.Tools.Rebus's activity source and meter name, kept as a literal so native-only hosts don't carry Rebus.
    private const string _rebusInstrumentationName = "ark.tools.rebus";
    // Ark.Tools.Outbox's activity source and meter name, kept as a literal for the same reason.
    private const string _outboxInstrumentationName = "ark.tools.outbox";
    // .NET runtime and process metrics, emitted natively by .NET 9 and later.
    private const string _runtimeMeterName = "System.Runtime";
    private const string _adaptiveSamplerSection = "OTel:ArkAdaptiveSampler";

    /// <summary>
    /// Adds the Ark sampler, processors and client instrumentations to an exporter-agnostic OpenTelemetry builder.
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
    /// section, and SQL spans to the NLog database (<c>ConnectionStrings:NLog.Database</c>) are dropped.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The tracing provider owns and disposes registered processors.")]
    public static OpenTelemetryBuilder AddArkOpenTelemetry(
        this OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        Action<ArkOtelConfig>? configureArkOtel = null,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler = null,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var arkOtelConfig = new ArkOtelConfig();
        configureArkOtel?.Invoke(arkOtelConfig);
        var skippedSqlQueryLabels = _getSkippedSqlQueryLabels(arkOtelConfig.SqlQueryLabelsToSkip);
        var failedTraceRegistry = new FailedTraceRegistry();
        // No-op when no NLog database is configured.
        var nlogDatabaseFilter = new ArkSqlDependencyFilterProcessor(_getNLogConnectionString(configuration));
        ArkAdaptiveSampler? sampler = null;

        return builder
            .ConfigureResource(static resource => resource.AddArkTelemetryResource())
            .WithTracing(tracing => tracing
                .ConfigureServices(services =>
                {
                    services.AddOptions<ArkAdaptiveSamplerOptions>().Configure(options =>
                    {
                        configuration?.GetSection(_adaptiveSamplerSection).Bind(options);
                        configureAdaptiveSampler?.Invoke(options);
                    });
                    services.AddSingleton(sp => sampler = new ArkAdaptiveSampler(
                        sp.GetRequiredService<IOptions<ArkAdaptiveSamplerOptions>>().Value,
                        failedTraceRegistry)
                    {
                        _sqlDependencyFilter = nlogDatabaseFilter,
                    });
                })
                .AddProcessor(new ArkPreFilterProcessor())
                .SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>())
                .AddSource(_rebusInstrumentationName)
                .AddSource(_mediatorMessagingInstrumentationName)
                .AddSource(_outboxInstrumentationName)
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation(options =>
                {
                    options.RecordException = true;
                    configureSqlClient?.Invoke(options);
                    var applicationFilter = options.Filter;
                    options.Filter = command =>
                    {
                        var include = _includeSqlClientSpan(command, skippedSqlQueryLabels)
                            && (applicationFilter is null || applicationFilter(command));
                        // The filter runs after sampling, on the span just started: give back its budget.
                        if (!include && Activity.Current is { } activity)
                            sampler?._release(activity);
                        return include;
                    };
                    var applicationEnricher = options.EnrichWithSqlCommand;
                    options.EnrichWithSqlCommand = (activity, command) =>
                    {
                        applicationEnricher?.Invoke(activity, command);
                        if (command is DbCommand dbCommand)
                            ArkSqlQueryLabel.SetTag(activity, dbCommand.CommandText);
                    };
                })
                .AddProcessor(new ArkSqlClientSpanProcessor(arkOtelConfig.IncludeSqlQueryText))
                .AddProcessor(new WebApi4xxAsSuccessProcessor())
                .AddProcessor(new ArkFailurePromotionProcessor(failedTraceRegistry))
                .AddProcessor(nlogDatabaseFilter)
                .AddArkComplianceRedaction(arkOtelConfig.ComplianceRedaction))
            .WithMetrics(static metrics => metrics
                .AddMeter(_rebusInstrumentationName)
                .AddMeter(_mediatorMessagingInstrumentationName)
                .AddMeter(_outboxInstrumentationName)
                .AddMeter(_runtimeMeterName)
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation());
    }

    // Same lookup as Ark.Tools.NLog's GetNLogSetting: hosts may turn the '.' into ':' or '_'.
    private static string? _getNLogConnectionString(IConfiguration? configuration)
    {
        return configuration?["ConnectionStrings:NLog.Database"]
            ?? configuration?["ConnectionStrings:NLog:Database"]
            ?? configuration?["ConnectionStrings:NLog_Database"];
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

        foreach (var label in sqlQueryLabelsToSkip.Where(static label => !string.IsNullOrWhiteSpace(label)))
            labels.Add(label.Trim());

        return labels;
    }
}
