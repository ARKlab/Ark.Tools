using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Ark.Tools.ResourceWatcher.WorkerHost;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using TestWorker.DataProvider;
using TestWorker.Dto;
using TestWorker.Host;
using TestWorker.Configs;
using System;
using Microsoft.Extensions.Logging;

namespace TestWorker
{
    public static class Extensions
    {
        public static IHostBuilder AddWorkerHostInfrastracture(this IHostBuilder builder)
        {
            return builder.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddJsonFile("appsettings.json", true);
                cfg.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", true);
                cfg.AddEnvironmentVariables();
                //cfg.AddCommandLine(args);
            })
            .ConfigureLogging((context, b) =>
            {
                b.SetMinimumLevel(LogLevel.Debug);
                b.AddConsole();
                b.AddAzureWebAppDiagnostics();
            });
        }

        public static IHostBuilder AddWorkerHost<T>(this IHostBuilder builder, Func<T, T> configHost)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddSingleton(s => configHost);

                //services.AddSingleton<IHostedService,
                //    HostServiceWrap<Test_Host.Host, Test_File, Test_FileMetadataDto, Test_ProviderFilter>>();
            })
            .UseConsoleLifetime();
        }

        public static IHostBuilder AddWorkerHost2(this IHostBuilder builder)
        {
            return builder.ConfigureServices(services =>
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
        }

        //public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
        //{
        //    return builder.ConfigureServices((ctx, services) =>
        //    {
        //        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        //        //TelemetryChannel
        //        services.TryAddSingleton<ITelemetryChannel, ServerTelemetryChannel>();

        //        //TelemetryModule
        //        services.AddSingleton<ITelemetryModule, PerformanceCollectorModule>();
        //        services.AddSingleton<ITelemetryModule, DependencyTrackingTelemetryModule>();
        //        services.AddSingleton<ITelemetryModule, QuickPulseTelemetryModule>();
        //        services.AddSingleton<TelemetryConfiguration>(provider => provider.GetService<IOptions<TelemetryConfiguration>>().Value);

        //        services.AddSingleton<ITelemetryModule, AppServicesHeartbeatTelemetryModule>();
        //        services.AddSingleton<ITelemetryModule, AzureInstanceMetadataTelemetryModule>();

        //        services.ConfigureTelemetryModule<DependencyTrackingTelemetryModule>((module, o) =>
        //        {
        //            module.EnableLegacyCorrelationHeadersInjection =
        //                o.DependencyCollectionOptions.EnableLegacyCorrelationHeadersInjection;

        //            var excludedDomains = module.ExcludeComponentCorrelationHttpHeadersOnDomains;
        //            excludedDomains.Add("core.windows.net");
        //            excludedDomains.Add("core.chinacloudapi.cn");
        //            excludedDomains.Add("core.cloudapi.de");
        //            excludedDomains.Add("core.usgovcloudapi.net");

        //            if (module.EnableLegacyCorrelationHeadersInjection)
        //            {
        //                excludedDomains.Add("localhost");
        //                excludedDomains.Add("127.0.0.1");
        //            }

        //            var includedActivities = module.IncludeDiagnosticSourceActivities;
        //            includedActivities.Add("Microsoft.Azure.EventHubs");
        //            includedActivities.Add("Microsoft.Azure.ServiceBus");

        //            module.EnableW3CHeadersInjection = o.RequestCollectionOptions.EnableW3CDistributedTracing;
        //        });

        //        services.AddSingleton<ITelemetryModule, DeveloperModeWithDebuggerAttachedTelemetryModule>();

        //        services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();


        //        services.TryAddSingleton<IApplicationIdProvider, ApplicationInsightsApplicationIdProvider>();
        //        services.AddSingleton<TelemetryClient>();

        //        services.AddSingleton<IHostedService, ApplicationInsightsStarter>();

        //        services.AddOptions();
        //        services.AddSingleton<IConfigureOptions<TelemetryConfiguration>, TelemetryConfigurationOptionsSetup>();

        //        services.Configure<ApplicationInsightsServiceOptions>(o =>
        //        {
        //            o.InstrumentationKey = "fef8ed59-fc07-4890-865f-edba7d8d41f9"; // ctx.Configuration["ApplicationInsights:InstrumentationKey"];
        //            o.EnableAdaptiveSampling = true;
        //            o.EnableHeartbeat = true;
        //            o.AddAutoCollectedMetricExtractor = true;
        //            o.DeveloperMode = Debugger.IsAttached;
        //        });

        //    });
        //}
    }
}
