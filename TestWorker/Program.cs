using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Ark.Tools.ResourceWatcher.WorkerHost;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;
using TestWorker.DataProvider;
using TestWorker.Dto;
using TestWorker.Host;
using TestWorker.Configs;
using System.Diagnostics;

namespace TestWorker
{
    class HostServiceWrap<T, TResource, TMetadata, TQueryFilter> : IHostedService
        where T : WorkerHost<TResource, TMetadata, TQueryFilter>
        where TResource : class, IResource<TMetadata>
        where TMetadata : class, IResourceMetadata
        where TQueryFilter : class, new()
    {
        private readonly T _host;

        public HostServiceWrap(T host)
        {
            _host = host;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => _host.Start(), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => _host.Stop(), cancellationToken);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var builder = new HostBuilder()
                .ConfigureAppConfiguration((ctx,cfg) => 
                {
                    cfg.AddJsonFile("appsettings.json", true);
                    cfg.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", true);
                    cfg.AddEnvironmentVariables();
                    cfg.AddCommandLine(args);
                })
                .ConfigureLogging((context, b) =>
                {
                    b.SetMinimumLevel(LogLevel.Debug);
                    b.AddConsole();
                    b.AddAzureWebAppDiagnostics();                    
                })
                .ConfigureServices((ctx,services) =>
                {
                    
                    services.TryAddSingleton<ITelemetryChannel, ServerTelemetryChannel>();
                    services.AddSingleton<ITelemetryModule, PerformanceCollectorModule>();
                    services.AddSingleton<ITelemetryModule, DependencyTrackingTelemetryModule>();
                    services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>();
                    services.AddSingleton<TelemetryConfiguration>(provider =>
                        provider.GetService<IOptions<TelemetryConfiguration>>().Value);

                    services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();
                    services.AddSingleton<ITelemetryModule, AzureInstanceMetadataTelemetryModule>();

                    services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();
                    
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
                    

                    services.AddSingleton<TelemetryClient>();

                    services.AddOptions();

                    services.AddSingleton<IHostedService, ApplicationInsightsStarter>();
                    services.Configure<ApplicationInsightsServiceOptions>(o =>
                    {
                        o.InstrumentationKey = "5312a6ba-7995-488e-b84a-c445810b5bcc"; // ctx.Configuration["ApplicationInsights:InstrumentationKey"];
                        o.EnableAdaptiveSampling = true;
                        o.EnableHeartbeat = true;
                        o.AddAutoCollectedMetricExtractor = true;
                        o.DeveloperMode = Debugger.IsAttached;                        
                    });
                    services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, TelemetryConfigurationOptionsSetup>();
                })
                .ConfigureServices(services =>
                {
                    // add some sample services to demonstrate job class DI

                    services.AddSingleton(s =>
                    {
                        var config = s.GetService<IConfiguration>();

                        var baseCfg = new Test_Host_Config()
                        {
                            StateDbConnectionString = config.GetConnectionString("boh")
                        };
                        
                        return new Test_Host.Host(baseCfg)
                            .WithTestWriter();
                    });

                    services.AddSingleton<IHostedService, 
                        HostServiceWrap<Test_Host.Host, Test_File, Test_FileMetadataDto, Test_ProviderFilter>>();
                })
                .UseConsoleLifetime();

            var host = builder.Build();
            using (host)
            {
                host.Start();
                host.WaitForShutdown();
            }


            /*

        var cfg = TelemetryConfiguration.CreateDefault();
        var listener = new ResourceWatcherDiagnosticListener(cfg);

        Test_Host
            .ConfigureFromAppSettings()
            .Start();


        Thread.Sleep(Timeout.Infinite);
        */
        }
    }

    internal class ApplicationInsightsStarter : IHostedService
    {
        public ApplicationInsightsStarter(TelemetryConfiguration telemetryConfiguration)
        {

        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
