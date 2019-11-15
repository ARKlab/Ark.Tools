using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Microsoft.AspNetCore;
using Ark.Tools.Nodatime;

namespace ProblemDetailsSample
{
    public static class Program
    {
        public static IWebHostBuilder GetWebHostBuilder(string[] args)
        {
            args = args ?? Array.Empty<string>();

            var host = WebHost.CreateDefaultBuilder(args).Config(args);
            ;

            return host;
        }

        public static IWebHostBuilder Config(this IWebHostBuilder builder, string[] args)
        {
            return builder
                .UseKestrel(o =>
                {
                    o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
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
                        .AddApplicationInsightsSettings(developerMode: env.IsDevelopment())
                        .AddCommandLine(args)
                        ;
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddDebug();
                })
                ;
        }

        public static void InitStatic(string[] args)
        {
            args = args ?? Array.Empty<string>();

            //DapperNodaTimeSetup.Register();
            NodeTimeConverter.Register();
            ServicePointManager.UseNagleAlgorithm = true;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.CheckCertificateRevocationList = true;
            ServicePointManager.DefaultConnectionLimit = 250;
            ServicePointManager.EnableDnsRoundRobin = true;
            ServicePointManager.DnsRefreshTimeout = 4 * 60 * 1000;

            var cfg = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build()
                ;

            //var ns = "ProblemDetailsSample";
            //NLogConfigurer.For(ns)
            //   .WithDefaultTargetsAndRules(ns.Replace('.', '_'), cfg.GetConnectionString("NLog.Database"), cfg["NLog:NotificationList"]
            //               , cfg["NLog:Smtp:Server"], Convert.ToInt32(cfg["NLog:Smtp:Port"]), cfg["NLog:Smtp:Username"], cfg["NLog:Smtp:Password"], Convert.ToBoolean(cfg["NLog:Smtp:UseSsl"]))
            //               //.WithDatabaseRule(@"*", NLog.LogLevel.Trace)
            //               .Apply();

            //if (Debugger.IsAttached)
            //{
            //    LogManager.Configuration.AddTarget("Debugger", new DebuggerTarget("Debugger"));
            //    LogManager.Configuration.AddRuleForAllLevels("Debugger");
            //}
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //AppDomain.CurrentDomain.UnhandledException += (s, e) => NLog.LogManager.GetLogger("Main").Fatal(e.ExceptionObject as Exception, "UnhandledException");

                InitStatic(args);

                using (var h = GetWebHostBuilder(args)
                    .UseIISIntegration()
                    .UseStartup<Startup>()
                    .Build())
                    await h.RunAsync();
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
}
