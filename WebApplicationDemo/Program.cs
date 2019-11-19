using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebApplicationDemo
{
	public static class Program
	{
		//public static IHostBuilder GetHostBuilder(string[] args)
		//{
		//	args = args ?? Array.Empty<string>();

		//	var host = Host.CreateDefaultBuilder(args).Config(args);

		//	return host;
		//}

		//public static IHostBuilder Config(this IHostBuilder builder, string[] args)
		//{
		//	return builder
		//		//.UseContentRoot(Directory.GetCurrentDirectory())
		//		.ConfigureWebHostDefaults(webBuilder =>
		//		{
		//			webBuilder.UseKestrel(o =>
		//			{
		//				//o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
		//			})
		//			.UseIISIntegration()
		//			.UseStartup<Startup>();

		//			//.UseSetting(HostDefaults.DetailedErrorsKey, "true")
		//			//.UseSetting(HostDefaults.PreventHostingStartupKey, "true")
		//		});
		//}

		//public static void InitStatic(string[] args)
		//{
		//	args = args ?? Array.Empty<string>();
		//}

		//public static async Task Main(string[] args)
		//{
		//	try
		//	{
		//		InitStatic(args);

		//		using (var h = GetHostBuilder(args)
		//			.Build())
		//		{
		//			await h.RunAsync();
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		NLog.LogManager.GetLogger("Main").Fatal(ex, $@"Unhandled Fatal Exception occurred: {ex.Message}");
		//		Console.WriteLine(ex.ToString());
		//	}
		//	finally
		//	{
		//		NLog.LogManager.GetLogger("Main").Info("Shutting down");
		//		NLog.LogManager.Flush();
		//	}
		//}


		//***************** DEFAULT *******************/
		
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>();
				});
	}
}
