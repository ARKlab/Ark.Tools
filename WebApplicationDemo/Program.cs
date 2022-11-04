using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Ark.Tools.NLog;
using Ark.Tools.Nodatime;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NLog.Extensions.Logging;

namespace WebApplicationDemo
{
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
					.ConfigureLogging((ctx, logging) =>
                    {
                        var ns = "WebApplicationDemo";

                        NLogConfigurer.For(ns)
                            .WithDefaultTargetsAndRulesFromConfiguration(ctx.Configuration)
                            .Apply();

                        logging.ClearProviders();
                        logging.AddNLog();
					})
					.UseStartup<Startup>();
				});
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

		}

		public static async Task Main(string[] args)
		{
			try
			{
				InitStatic(args);

				using (var h = GetHostBuilder(args)
					.Build())
				{
					await h.RunAsync();
				}
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
}
