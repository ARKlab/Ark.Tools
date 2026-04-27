using Ark.Tools.ApplicationInsights.HostedService;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

public static partial class Ex
{
    /// <summary>
    /// Registers Application Insights for a worker host, including the
    /// <see cref="ResourceWatcherTelemetryModule"/> DiagnosticSource listener.
    /// </summary>
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection.")]
    public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
    {
        return builder
            .AddApplicationInsightsForHostedService()
            .ConfigureServices((ctx, services) =>
            {
                services.AddHostedService<ResourceWatcherTelemetryModule>();
            });
    }
}