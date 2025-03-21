using Ark.Tools.AspNetCore.ApplicationInsights;
using Ark.Tools.NLog;
using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;

using LinuxWebJobHosting;
using LinuxWebJobHosting.Utils;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NLog;

using System;
using System.Globalization;
using System.Threading.Tasks;

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
            _logger.Info(CultureInfo.InvariantCulture, "Starting program");
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
                    cfg.AddArkEnvironmentVariables();
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
                _logger.Info(CultureInfo.InvariantCulture, "Starting program.");
                InitStatic(args);

                using var h = GetHostBuilder(args)
                    .Build();
                await h.RunAsync();
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, $@"Unhandled Fatal Exception occurred: {ex.Message}");
            }
            finally
            {
                _logger.Info(CultureInfo.InvariantCulture, "Shutting down");
                NLog.LogManager.Flush();
            }
        }
    }
}