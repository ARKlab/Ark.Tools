// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

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
    private const string _azureCoreHttpSourceName = "Azure.Core.Http";

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
    /// Optional application configuration for the adaptive sampler.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkOpenTelemetry(
        this OpenTelemetryBuilder builder,
        Action<SqlClientTraceInstrumentationOptions>? configureSqlClient = null,
        Action<ArkOtelConfig>? configureArkOtel = null,
        Action<ArkAdaptiveSamplerOptions>? configureAdaptiveSampler = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var arkOtelConfig = new ArkOtelConfig();
        configureArkOtel?.Invoke(arkOtelConfig);
        var skippedSqlQueryLabels = _getSkippedSqlQueryLabels(arkOtelConfig.SqlQueryLabelsToSkip);
        var failedTraceRegistry = new FailedTraceRegistry();

        return builder
            .ConfigureResource(static resource => resource.AddArkTelemetryResource())
            .WithTracing(tracing => tracing
                .ConfigureServices(services =>
                {
                    var samplerOptions = services.AddOptions<ArkAdaptiveSamplerOptions>();
                    if (configureAdaptiveSampler is not null)
                        samplerOptions.Configure(configureAdaptiveSampler);
                    services.AddSingleton(sp => new ArkAdaptiveSampler(
                        sp.GetRequiredService<IOptions<ArkAdaptiveSamplerOptions>>().Value,
                        failedTraceRegistry));
                })
                .AddProcessor(new ArkPreFilterProcessor())
                .SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>())
                .AddSource(_rebusInstrumentationName)
                .AddSource(_mediatorMessagingInstrumentationName)
                .AddHttpClientInstrumentation(static options =>
                    options.FilterHttpRequestMessage = static _ => !_isAzureSdkHttpRequest())
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
                .AddProcessor(new WebApi4xxAsSuccessProcessor())
                .AddProcessor(new ArkFailurePromotionProcessor(failedTraceRegistry))
                .AddArkComplianceRedaction(arkOtelConfig.ComplianceRedaction))
            .WithMetrics(static metrics => metrics
                .AddMeter(_rebusInstrumentationName)
                .AddMeter(_mediatorMessagingInstrumentationName)
                .AddSqlClientInstrumentation());
    }

    // Ported from the Azure Monitor Distro: Azure SDKs already emit a client span for each HTTP call,
    // so the nested HttpClient span would duplicate it.
    private static bool _isAzureSdkHttpRequest()
    {
        return Activity.Current?.Parent?.Source.Name == _azureCoreHttpSourceName;
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
