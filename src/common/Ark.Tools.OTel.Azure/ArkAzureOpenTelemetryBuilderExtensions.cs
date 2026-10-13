// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

using System.Diagnostics;

namespace Ark.Tools.OTel.Azure;

/// <summary>
/// Azure SDK tracing and Azure Monitor export for the Ark OpenTelemetry setups, on any host.
/// </summary>
public static class ArkAzureOpenTelemetryBuilderExtensions
{
    private const string _azureActivitySourceSwitch = "Azure.Experimental.EnableActivitySource";
    private const string _azureCoreHttpSourceName = "Azure.Core.Http";

    /// <summary>
    /// Adds Azure SDK tracing to an Ark OpenTelemetry setup: the <c>Azure.*</c> sources (Service Bus included),
    /// the experimental Azure SDK ActivitySource switch, the deduplication of the <c>HttpClient</c> span nested under
    /// an Azure SDK span, and a sampling filter for Service Bus receives.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder, already configured with an Ark setup.</param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAzureSdkInstrumentation(this OpenTelemetryBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Azure SDKs emit Service Bus and Event Hubs spans only with this experimental switch on. The package's
        // buildTransitive props set it in the runtime configuration; this covers project references.
        // An explicit application or environment choice wins.
        if (!AppContext.TryGetSwitch(_azureActivitySourceSwitch, out _)
            && Environment.GetEnvironmentVariable("AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE") is null)
        {
            AppContext.SetSwitch(_azureActivitySourceSwitch, true);
        }

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IArkSamplingFilter, AzureServiceBusReceiveSamplingFilter>());

        // Azure SDKs already emit a client span for each HTTP call: drop the nested HttpClient span, as the Azure
        // Monitor Distro does. PostConfigure runs after every filter assignment, the Distro's included, so the
        // application's filters set before and after this call still apply.
        Func<HttpRequestMessage, bool>? filterBefore = null;
        builder.Services.Configure<HttpClientTraceInstrumentationOptions>(options =>
            filterBefore = options.FilterHttpRequestMessage);
        builder.Services.PostConfigure<HttpClientTraceInstrumentationOptions>(options =>
        {
            var before = filterBefore;
            var after = options.FilterHttpRequestMessage;
            options.FilterHttpRequestMessage = request =>
                Activity.Current?.Parent?.Source.Name != _azureCoreHttpSourceName
                && (before is null || before(request))
                && (after is null || ReferenceEquals(after, before) || after(request));
        });

        return builder.WithTracing(static tracing => tracing.AddSource("Azure.*"));
    }

    /// <summary>
    /// Adds Azure SDK tracing (see <see cref="AddArkAzureSdkInstrumentation"/>) to an Ark OpenTelemetry setup and,
    /// when an Application Insights connection string is configured, exports traces, metrics and error logs to
    /// Azure Monitor. The Ark adaptive sampler stays in charge of sampling.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder, already configured with an Ark setup.</param>
    /// <param name="configuration">
    /// Optional application configuration holding <c>ApplicationInsights:ConnectionString</c> or
    /// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>; the environment variable is read as a fallback.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAzureOpenTelemetry(
        this OpenTelemetryBuilder builder,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddArkAzureSdkInstrumentation();

        var connectionString = configuration?["ApplicationInsights:ConnectionString"]
            ?? configuration?["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return builder;

        builder.Services.AddLogging(static logging =>
            logging.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error));
        builder.UseAzureMonitorExporter(options => options.ConnectionString = connectionString);

        // UseAzureMonitorExporter sets its own sampler; set Ark's again so the later registration wins.
        return builder.WithTracing(static tracing =>
            tracing.SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>()));
    }
}
