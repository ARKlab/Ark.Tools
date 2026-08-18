using Ark.Tools.NLog;

using Azure.Monitor.OpenTelemetry.Profiler;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Reflection;

namespace Ark.Tools.ApplicationInsights.HostedService;

public static partial class Ex
{
    /// <summary>
    /// Adds the Application Insights worker service, profiler, and Ark OpenTelemetry customizations.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The configured host builder.</returns>
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection.")]
    public static IHostBuilder AddApplicationInsightsForHostedService(this IHostBuilder builder)
    {
        return builder.ConfigureServices((ctx, services) =>
        {
            // Resolve connection string from configuration
            var connectionString = ctx.Configuration["ApplicationInsights:ConnectionString"]
                ?? ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
            var instrumentationKey = ctx.Configuration["ApplicationInsights:InstrumentationKey"]
                ?? ctx.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"];

            // Check if we have a valid connection string or instrumentation key
            var hasValidConnectionString = !string.IsNullOrWhiteSpace(connectionString) || !string.IsNullOrWhiteSpace(instrumentationKey);

            services.AddApplicationInsightsTelemetryWorkerService(o =>
            {
                o.ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

                if (hasValidConnectionString)
                {
                    o.ConnectionString = connectionString ?? $"InstrumentationKey={instrumentationKey}";
                }
                else
                {
                    // When no connection string is provided (e.g., in tests or local development),
                    // use a dummy connection string to prevent SDK errors
                    o.ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
                }

                o.EnableDependencyTrackingTelemetryModule = true;
            });
            services.AddAzureMonitorProfiler();

            // Register the Ark adaptive sampler and custom processors on the OTel tracing pipeline.
            // This MUST be after AddApplicationInsightsTelemetryWorkerService.
            var sqlCs = ctx.Configuration.GetNLogSetting("ConnectionStrings:" + NLog.NLogDefaultConfigKeys.SqlConnStringName);
            services.AddArkApplicationInsightsCustomizations(ctx.Configuration, sqlCs);
        });
    }
}