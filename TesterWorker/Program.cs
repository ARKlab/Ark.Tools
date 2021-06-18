using Ark.Tools.ApplicationInsights.HostedService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace TesterWorker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            //.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            //.AddJsonFile($"appsettings.Test.json", optional: true)
            //.AddJsonFile($"appsettings.Ibrid.json", optional: true)
            .AddJsonFile($"appsettings.Development.json", optional: true)
            //.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
            .AddEnvironmentVariables()
            .Build()
            ;


            CreateHostBuilder(args, configuration)
            .Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, IConfiguration config) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(builder => builder.AddConfiguration(config))
                .AddApplicationInsightsForHostedService()
                .ConfigureServices((hostContext, services) =>
                {
                    //services.Configure<SamplingPercentageEstimatorSettings>(o =>
                    //{
                    //    o.MaxTelemetryItemsPerSecond = 1000;
                    //});

                    services.AddHostedService<Worker>();
                });
    }
}
