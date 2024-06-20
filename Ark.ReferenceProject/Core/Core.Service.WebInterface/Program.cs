using Ark.Tools.NLog;

using Core.Service.WebInterface.Utils;

using Ark.Reference.Common;

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

namespace Core.Service.WebInterface
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
                .ConfigureNLog("Core.Service.WebInterface")
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

                        if (Environment.GetEnvironmentVariable("LOCAL_DEBUG") == "True"
                            || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
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

                using (var h = GetHostBuilder(args).Build())

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
                NLog.LogManager.Flush();
            }
        }
    }
}
