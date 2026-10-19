// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Monitor.OpenTelemetry.Exporter;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using System.Diagnostics;
using System.Reflection;

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
        // buildTransitive props set it in the runtime configuration of package consumers; this covers project
        // references, which do not import the props.
        // An explicit application or environment choice wins.
        if (!AppContext.TryGetSwitch(_azureActivitySourceSwitch, out _)
            && Environment.GetEnvironmentVariable("AZURE_EXPERIMENTAL_ENABLE_ACTIVITY_SOURCE") is null)
        {
            AppContext.SetSwitch(_azureActivitySourceSwitch, true);
        }

        // Service Bus receives do not use the sampler budget; after every Configure, so other filters are kept.
        builder.Services.PostConfigure<ArkAdaptiveSamplerOptions>(static options =>
        {
            var filter = options.Filter;
            options.Filter = p => !AzureSamplingFilters.IsServiceBusReceive(p) && (filter is null || filter(p));
        });

        // Azure SDKs already emit a client span for each HTTP call: drop the nested HttpClient span, as the Azure
        // Monitor Distro does. PostConfigure runs after every filter assignment, so the application's filters set
        // before and after this call still apply.
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
    /// Adds the Azure integration to an Ark OpenTelemetry setup, on any host: Azure SDK tracing
    /// (see <see cref="AddArkAzureSdkInstrumentation"/>), the Azure App Service, Virtual Machine and Container Apps
    /// resource detectors and, when an Application Insights connection string is configured, the export of traces,
    /// metrics (live metrics included) and error logs to Azure Monitor. The Ark adaptive sampler stays in charge of
    /// sampling.
    /// </summary>
    /// <param name="builder">The OpenTelemetry builder, already configured with an Ark setup.</param>
    /// <param name="configuration">
    /// Optional application configuration holding <c>ApplicationInsights:ConnectionString</c> or
    /// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>; the environment variable is read as a fallback. The adaptive
    /// sampler also reads the legacy <c>ApplicationInsights:ArkAdaptiveSampler</c> section, overridden by
    /// <c>OTel:ArkAdaptiveSampler</c>.
    /// </param>
    /// <param name="clientErrorsAsSuccess">
    /// When Azure Monitor is enabled, sets <see cref="ActivityStatusCode.Ok"/> on server spans that end with an HTTP
    /// 4xx code and an <see cref="ActivityStatusCode.Unset"/> status, so Application Insights shows them as successful
    /// requests. Spans marked <see cref="ActivityStatusCode.Error"/> stay failed. Set to <see langword="false"/> to
    /// keep the exporter's default, which shows <see cref="ActivityStatusCode.Unset"/> 4xx requests as failed.
    /// </param>
    /// <returns>The original OpenTelemetry builder.</returns>
    public static OpenTelemetryBuilder AddArkAzureOpenTelemetry(
        this OpenTelemetryBuilder builder,
        IConfiguration? configuration = null,
        bool clientErrorsAsSuccess = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (configuration is not null)
        {
            // First in the collection, so the Ark setup's OTel:ArkAdaptiveSampler section and callback, registered
            // earlier, still win.
            builder.Services.Insert(0, ServiceDescriptor.Singleton<IConfigureOptions<ArkAdaptiveSamplerOptions>>(
                new ConfigureNamedOptions<ArkAdaptiveSamplerOptions>(
                    Options.DefaultName,
                    options => configuration.GetSection("ApplicationInsights:ArkAdaptiveSampler").Bind(options))));
        }

        builder
            .AddArkAzureSdkInstrumentation()
            // Cloud role instance and Azure attributes when running on App Service, a VM or Container Apps.
            .ConfigureResource(static resource =>
            {
                resource
                    .AddAzureAppServiceDetector()
                    .AddAzureVMDetector()
                    .AddAzureContainerAppsDetector();

                // The detectors set service.name to the site or app name: keep Ark's entry assembly name, which tells
                // apart the processes of one site, such as a web app and its WebJobs.
                var serviceName = Assembly.GetEntryAssembly()?.GetName().Name;
                if (!string.IsNullOrWhiteSpace(serviceName))
                    resource.AddAttributes([new("service.name", serviceName)]);
                // OTEL_SERVICE_NAME and OTEL_RESOURCE_ATTRIBUTES still win.
                resource.AddEnvironmentVariableDetector();
            });

        var connectionString = configuration?["ApplicationInsights:ConnectionString"]
            ?? configuration?["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return builder;

        builder.Services.AddLogging(static logging =>
            logging.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error));
        // After the Ark chain and the processors registered so far; the exporter adds its own processors when the
        // host starts, so they run after this one.
        if (clientErrorsAsSuccess)
            builder.WithTracing(static tracing => tracing.AddProcessor(new AzureMonitorClientErrorsAsSuccessProcessor()));
        builder.UseAzureMonitorExporter(options => options.ConnectionString = connectionString);

        // UseAzureMonitorExporter sets its own sampler; set Ark's again so the later registration wins.
        return builder.WithTracing(static tracing =>
            tracing.SetSampler(static services => services.GetRequiredService<ArkAdaptiveSampler>()));
    }
}
