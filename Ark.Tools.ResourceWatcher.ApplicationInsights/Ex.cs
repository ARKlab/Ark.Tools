using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ark.Tools.ApplicationInsights.HostedService;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
        {
            return builder
                .AddApplicationInsightsForHostedService()
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();
                });
        }
    }
}
