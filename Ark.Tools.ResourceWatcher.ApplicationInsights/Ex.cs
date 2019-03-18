using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Extensibility.Implementation.ApplicationId;
using Microsoft.AspNetCore.Http;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
        {
            return builder.ConfigureServices((ctx, services) =>
            {
                services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

                //TelemetryChannel
                services.TryAddSingleton<ITelemetryChannel, ServerTelemetryChannel>();

                //TelemetryModule
                services.AddSingleton<ITelemetryModule, PerformanceCollectorModule>();
                services.AddSingleton<ITelemetryModule, DependencyTrackingTelemetryModule>();
                services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>();
                services.AddSingleton<TelemetryConfiguration>(provider => provider.GetService<IOptions<TelemetryConfiguration>>().Value);

                services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();
                services.AddSingleton<ITelemetryModule, AzureInstanceMetadataTelemetryModule>();

                services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
                {
                    module.EnableLegacyCorrelationHeadersInjection =
                        o.DependencyCollectionOptions.EnableLegacyCorrelationHeadersInjection;

                    var excludedDomains = module.ExcludeComponentCorrelationHttpHeadersOnDomains;
                    excludedDomains.Add("core.windows.net");
                    excludedDomains.Add("core.chinacloudapi.cn");
                    excludedDomains.Add("core.cloudapi.de");
                    excludedDomains.Add("core.usgovcloudapi.net");

                    if (module.EnableLegacyCorrelationHeadersInjection)
                    {
                        excludedDomains.Add("localhost");
                        excludedDomains.Add("127.0.0.1");
                    }

                    var includedActivities = module.IncludeDiagnosticSourceActivities;
                    includedActivities.Add("Microsoft.Azure.EventHubs");
                    includedActivities.Add("Microsoft.Azure.ServiceBus");

                    module.EnableW3CHeadersInjection = o.RequestCollectionOptions.EnableW3CDistributedTracing;
                });

                services.AddSingleton<ITelemetryModule, DeveloperModeWithDebuggerAttachedTelemetryModule>();

                services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();


                services.TryAddSingleton<IApplicationIdProvider, ApplicationInsightsApplicationIdProvider>();
                services.AddSingleton<TelemetryClient>();

                services.AddSingleton<IHostedService, ApplicationInsightsStarter>();

                services.AddOptions();
                services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, TelemetryConfigurationOptionsSetup>();

                services.Configure<ApplicationInsightsServiceOptions>(o =>
                {
                    o.InstrumentationKey = ctx.Configuration["ApplicationInsights:InstrumentationKey"];
                    o.EnableAdaptiveSampling = true;
                    o.EnableHeartbeat = true;
                    o.AddAutoCollectedMetricExtractor = true;
                    o.DeveloperMode = Debugger.IsAttached;
                });

            });
        }
    }
}
