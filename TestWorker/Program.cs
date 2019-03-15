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
                .AddWorkerHostInfrastracture()
                .AddApplicationInsightsForWorkerHost()
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
}
