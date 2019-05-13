using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Raven.Embedded;
using System;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public static class Program
	{
		public static IWebHostBuilder GetWebHostBuilder(string[] args)
		{
			args = args ?? Array.Empty<string>();

			var host = WebHost.CreateDefaultBuilder(args).Config(args);
			
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
				//.ConfigureAppConfiguration((hostingContext, config) =>
				//{
				//	var env = hostingContext.HostingEnvironment;

				//	config
				//		.SetBasePath(Directory.GetCurrentDirectory())
				//		.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				//		.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				//		.AddEnvironmentVariables()
				//		.AddApplicationInsightsSettings(developerMode: env.IsDevelopment())
				//		.AddCommandLine(args)
				//		;
				//})
				//.ConfigureLogging((hostingContext, logging) =>
				//{
				//	logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
				//	logging.AddConsole();
				//	logging.AddDebug();
				//})
				;
		}

		public static void InitStatic(string[] args)
		{
			args = args ?? Array.Empty<string>();
		}

		public static async Task Main(string[] args)
		{
			try
			{
				InitStatic(args);

				if (Environment.GetEnvironmentVariable("RAVENDB_EMBEDDED") == "True")
				{
					EmbeddedServer.Instance.StartServer(new ServerOptions
					{
						ServerUrl = "http://127.0.0.1:8080"
					});

					var store = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions("RavenDb"));
				}

				using (var h = GetWebHostBuilder(args)
					.UseIISIntegration()
					.UseStartup<Startup>()
					.Build())
					await h.RunAsync();
			}
			catch (Exception ex)
			{
				throw new Exception("Program error: ", ex);
			}
			finally
			{

			}
		}
	}
}
