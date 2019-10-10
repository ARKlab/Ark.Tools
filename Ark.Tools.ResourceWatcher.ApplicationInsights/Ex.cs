using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System;
using System.Reflection;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
        {
            return builder.ConfigureServices((ctx, services) =>
            {
                services.AddApplicationInsightsTelemetryWorkerService(o =>
                {
                    o.AddAutoCollectedMetricExtractor = true;
                    o.ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version.ToString();
                    o.InstrumentationKey = ctx.Configuration["ApplicationInsights:InstrumentationKey"] ?? Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
                    o.EnableAdaptiveSampling = true;
                    o.EnableHeartbeat = true;
                    o.AddAutoCollectedMetricExtractor = true;
                    o.DeveloperMode = Debugger.IsAttached;
                });
                services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();

            });
        }
    }
}
