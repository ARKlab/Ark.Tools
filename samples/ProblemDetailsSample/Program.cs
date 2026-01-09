using Ark.Tools.Nodatime;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.IO;
using System.Threading.Tasks;

namespace ProblemDetailsSample;

public static class Program
{
    public static IHostBuilder GetHostBuilder(string[] args)
    {
        args = args ?? Array.Empty<string>();

        var host = Host.CreateDefaultBuilder(args).Config(args);

        return host;
    }

    public static IHostBuilder Config(this IHostBuilder builder, string[] args)
    {
        return builder
            //.UseContentRoot(Directory.GetCurrentDirectory())
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(serverOptions =>
                {
                    // Set properties and call methods on options
                })
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
                        .AddCommandLine(args)
                        ;
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                })
                .UseIISIntegration()
                .UseStartup<Startup>();
            });
    }


    public static void InitStatic(string[] args)
    {
        args = args ?? Array.Empty<string>();

        //DapperNodaTimeSetup.Register();
        NodeTimeConverter.Register();

        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build()
            ;
    }

    public static async Task Main(string[] args)
    {
        try
        {
            InitStatic(args);

            using var h = GetHostBuilder(args)
                .Build();
            await h.RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            NLog.LogManager.GetLogger("Main").Fatal(ex, $@"Unhandled Fatal Exception occurred: {ex.Message}");
        }
        finally
        {
            NLog.LogManager.GetLogger("Main").Info("Shutting down");
            NLog.LogManager.Flush();
        }
    }
}