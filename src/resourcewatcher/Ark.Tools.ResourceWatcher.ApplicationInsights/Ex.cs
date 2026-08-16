using Ark.Tools.ApplicationInsights.HostedService;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

/// <summary>
/// Application Insights compatibility setup for ResourceWatcher OpenTelemetry signals.
/// </summary>
public static partial class Ex
{
    /// <summary>
    /// Registers Application Insights for a worker host and enables the
    /// ResourceWatcher OpenTelemetry source in the Application Insights v3 pipeline.
    /// </summary>
    [RequiresUnreferencedCode("Application Insights configuration binding uses reflection.")]
    public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
    {
        var registrationAttempted = 0;

        return builder
            .AddApplicationInsightsForHostedService()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IConfigureOptions<TelemetryConfiguration>>(_ =>
                    new ConfigureNamedOptions<TelemetryConfiguration>(
                        Options.DefaultName,
                        configuration =>
                        {
                            if (Interlocked.Exchange(ref registrationAttempted, 1) != 0)
                            {
                                return;
                            }

                            try
                            {
                                configuration.ConfigureOpenTelemetryBuilder(otelBuilder =>
                                {
                                    otelBuilder.Services.ConfigureOpenTelemetryTracerProvider(
                                        tracing => tracing.AddSource(ResourceWatcherInstrumentation.ActivitySourceName));
                                    otelBuilder.Services.ConfigureOpenTelemetryMeterProvider(
                                        metrics => metrics.AddMeter(ResourceWatcherInstrumentation.MeterName));
                                });
                            }
                            catch (InvalidOperationException ex)
                                when (ex.Message.StartsWith(
                                    "Configuration cannot be modified after it has been built.",
                                    StringComparison.Ordinal))
                            {
                            }
                        }));
            });
    }
}