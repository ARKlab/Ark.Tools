using Ark.Tools.NLog;
using Ark.Tools.Nodatime;
using Ark.Tools.Nodatime.Dapper;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.IO;
using System.Threading.Tasks;

namespace WebApplicationDemo;

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
            .ConfigureServices(s =>
            {
                s.AddSingleton<IExternalInjected, ExternalInjected>();
            })
            //.UseContentRoot(Directory.GetCurrentDirectory())
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
                        .AddArkEnvironmentVariables()
                        .AddApplicationInsightsSettings(null, developerMode: env.IsDevelopment())
                        .AddCommandLine(args)
                        ;
                })
                .UseStartup<Startup>();
            })
            .ConfigureNLog();
    }

    public static void InitStatic(string[] args)
    {
        args = args ?? Array.Empty<string>();

        NodaTimeDapper.Setup();
        NodeTimeConverter.Register();

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
#pragma warning disable RS0030 // Exception handler - console output for critical failures
            Console.WriteLine(ex.ToString());
#pragma warning restore RS0030
        }
        finally
        {
            NLog.LogManager.GetLogger("Main").Info("Shutting down");
            NLog.LogManager.Flush();
        }
    }


    //***************** DEFAULT *******************/

    //public static void Main(string[] args)
    //{
    //	CreateHostBuilder(args).Build().Run();
    //}

    //public static IHostBuilder CreateHostBuilder(string[] args) =>
    //	Host.CreateDefaultBuilder(args)
    //		.ConfigureWebHostDefaults(webBuilder =>
    //		{
    //			webBuilder.UseStartup<Startup>();
    //		});
}

public interface IExternalInjected
{

}

public class ExternalInjected : IExternalInjected { }