using Ark.Tools.ApplicationInsights.HostedService;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.IO;
using System.Threading.Tasks;

namespace TesterWorker
{
    internal static class Program
    {
        public static async Task Main(string[] args)
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


            await CreateHostBuilder(args, configuration)
                .Build()
                .RunAsync();
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
