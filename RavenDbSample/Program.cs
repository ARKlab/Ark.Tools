using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Raven.Embedded;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RavenDbSample
{
	public static class Program
	{
		public static IConfiguration Configuration { get; internal set; }
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
					.UseIISIntegration()
					.UseStartup<Startup>();
				});
				//.ConfigureServices;
		}


		public static void InitStatic(string[] args)
		{
			args = args ?? Array.Empty<string>();

			Configuration = new ConfigurationBuilder()
			   .SetBasePath(Directory.GetCurrentDirectory())
			   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
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

				if (Environment.GetEnvironmentVariable("RAVENDB_EMBEDDED") == "True")
				{
					EmbeddedServer.Instance.StartServer(new ServerOptions
					{
						FrameworkVersion = "2.2.6",
						ServerUrl = "http://127.0.0.1:8080"
					});

					var store = EmbeddedServer.Instance.GetDocumentStore(new DatabaseOptions("RavenDb"));
				}

				using (var h = GetHostBuilder(args)
					.Build())
				{
					await h.RunAsync();
				}
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
