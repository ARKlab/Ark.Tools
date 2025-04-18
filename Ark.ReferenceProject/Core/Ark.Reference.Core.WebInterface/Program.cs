using Ark.Reference.Common;
using Ark.Reference.Core.WebInterface.Utils;
using Ark.Tools.NLog;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NLog.Extensions.Logging;

using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;

using System;
using System.IO;
using System.Threading.Tasks;

namespace Ark.Reference.Core.WebInterface
{
    public static class Program
    {
        public static IHostBuilder GetHostBuilder(string[] args)
        {
            args ??= Array.Empty<string>();

            var host = Host.CreateDefaultBuilder(args).Config(args);

            return host;
        }

        public static IHostBuilder Config(this IHostBuilder builder, string[] args)
        {
            return builder
                .ConfigureNLog("Ark.Reference.Core.WebInterface"
                // , configure: c => c.WithDatabaseRule("*", NLog.LogLevel.Info) // always log INFO to Database Target if present
                )
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                    .CaptureStartupErrors(true)
                    .UseSetting(WebHostDefaults.DetailedErrorsKey, "true")
                    .UseSetting(WebHostDefaults.PreventHostingStartupKey, "true")
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        var env = hostingContext.HostingEnvironment;

                        config
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                            .AddEnvironmentVariables()
                            .AddApplicationInsightsSettings(null, developerMode: env.IsDevelopment())
                            .AddCommandLine(args)// duplicated to let AKV url to be taken from CLI
                            .AddAzureKeyVaultMSI()
                            .AddCommandLine(args)
                            ;
                    })
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddSingleton<IHostedService, RebusProcessorService>();

                        // remember to add LOCAL_DEBUG to launchSetting.json when developing in local
                        if (Environment.GetEnvironmentVariable("LOCAL_DEBUG") == "True")
                        {
                            services.AddSingleton<InMemNetwork>();
                            services.AddSingleton<InMemorySubscriberStore>();
                        }
                    })
                    .UseIISIntegration()
                    .UseStartup<Startup>();
                });
        }


        public static async Task Main(string[] args)
        {
            try
            {
                GlobalInit.InitStatics();

                using var h = GetHostBuilder(args).Build();

                await h.RunAsync();
            }
            catch (Exception ex)
            {
                NLog.LogManager.GetLogger("Main").Fatal(ex, $@"Unhandled Fatal Exception occurred: {ex.Message}");
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                NLog.LogManager.GetLogger("Main").Info("Shutting down");
                NLog.LogManager.Shutdown();
            }
        }
    }
}
