using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using LinuxWebJobHosting;
using NLog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LinuxWebJobHosting.Utils;
using Ark.Tools.NLog;
using Ark.Tools.ApplicationInsights.HostedService;
using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;
using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Ark.Tools.AspNetCore.ApplicationInsights;

namespace Processor.Service.WebInterface
{
    public static class Program
    {
        private static readonly NLog.ILogger _logger = LogManager.GetCurrentClassLogger();
        public static IHostBuilder GetHostBuilder(string[] args)
        {
            args = args ?? Array.Empty<string>();

            var host = Host.CreateDefaultBuilder(args).Config(args);

            return host;
        }

        public static IHostBuilder Config(this IHostBuilder builder, string[] args)
        {
            _logger.Info("Starting program");
            return builder
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .CaptureStartupErrors(true)
                        .UseStartup<Startup>();
                })
                .AddApplicationInsithsTelemetryForWebHostArk()
                .ConfigureNLog()
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<IHostedService, HostedService>();
                }).ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddArkLegacyEnvironmentVariables();
                })
                .AddWorkerHost(
                    s =>
                    {
                        var cfg = s.GetRequiredService<IConfiguration>();
                        var h = TestWorker.HostNs.Test_Host.Configure(cfg, configurer: c =>
                        {
                        });

                        return h;
                    })
                ;

        }

        public static void InitStatic(string[] args)
        {
            
        }

        public static async Task Main(string[] args)
        {
            try
            {
                _logger.Info("Starting program.");
                InitStatic(args);

                using (var h = GetHostBuilder(args)
                    .Build())
                {
                    await h.RunAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, $@"Unhandled Fatal Exception occurred: {ex.Message}");
            }
            finally
            {
                _logger.Info("Shutting down");
                NLog.LogManager.Flush();
            }
        }
    }
}