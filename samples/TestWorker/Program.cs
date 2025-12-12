using Ark.Tools.NLog;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NLog.Extensions.Logging;

using TestWorker.Constants;

namespace TestWorker
{
    sealed class Program
    {
        static void Main(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .AddApplicationInsightsForWorkerHost()
                .ConfigureNLog(Test_Constants.AppName)
                .AddWorkerHost(
                    s =>
                    {
                        var cfg = s.GetRequiredService<IConfiguration>();
                        var h = HostNs.Test_Host.Configure(cfg, configurer: c =>
                        {
                            //c.IgnoreState = Debugger.IsAttached;
                        });

                        return h;
                    })
                .UseConsoleLifetime();

            hostBuilder.StartAndWaitForShutdown();
        }
    }
}
