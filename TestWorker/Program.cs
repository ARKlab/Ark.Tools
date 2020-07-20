using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;

namespace TestWorker
{
    class Program
    {
        static void Main(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .AddWorkerHostInfrastracture()
                .AddApplicationInsightsForWorkerHost()
                .AddWorkerHost(
                    s => {
                        var cfg = s.GetService<IConfiguration>();
                        var h = HostNs.Test_Host.Configure(cfg, configurer: c =>
                        {});

                return h;
            })
            .UseConsoleLifetime();

            hostBuilder.StartAndWaitForShutdown();
        }
    }
}
