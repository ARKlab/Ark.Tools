using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Microsoft.Extensions.DependencyInjection;
using Ark.Tools.NLog;
using TestWorker.Constants;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TestWorker
{
    class Program
    {
        static void Main(string[] args)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .AddApplicationInsightsForWorkerHost()
                .ConfigureNLog(Test_Constants.AppName)
                .AddWorkerHost(
                    s => {
                        var cfg = s.GetService<IConfiguration>();
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
