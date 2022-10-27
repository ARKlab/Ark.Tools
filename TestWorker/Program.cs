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
                .AddWorkerHostInfrastracture()
                .AddApplicationInsightsForWorkerHost()
                .ConfigureLogging((ctx,l) =>
                {

                    NLogConfigurer
                        .For(Test_Constants.AppName)
                        .WithDefaultTargetsAndRulesFromConfiguration(Test_Constants.AppName.Replace(".", ""), NLogConfigurer.MailFromDefault, ctx.Configuration)
                        .Apply();

                    l.ClearProviders();
                    l.AddNLog();
                })
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
