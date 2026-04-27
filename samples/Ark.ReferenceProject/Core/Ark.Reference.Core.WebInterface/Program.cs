using Ark.Reference.Common;
using Ark.Reference.Core.WebInterface.Utils;
using Ark.Tools.NLog;


using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;


namespace Ark.Reference.Core.WebInterface;

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
            .ConfigureNLog("Ark.Reference.Core.WebInterface"
            // , configure: c => c.WithDatabaseRule("*", NLog.LogLevel.Info) // always log INFO to Database Target if present
            )
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
                    if (!IsRunningUnderOpenApiGenerator())
                    {
                        services.AddSingleton<IHostedService, RebusProcessorService>();
                    }

                    // remember to add LOCAL_DEBUG to launchSetting.json when developing in local
                    if (Environment.GetEnvironmentVariable("LOCAL_DEBUG") == "True")
                    {
                        services.AddSingleton<InMemNetwork>();
                        services.AddSingleton<InMemorySubscriberStore>();
                    }
                })
                .UseIISIntegration()
                .UseStartup<Startup>();
            });
    }

    private static bool IsRunningUnderOpenApiGenerator()
    {
        var entryAssemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
        return string.Equals(entryAssemblyName, "GetDocument.Insider", StringComparison.Ordinal)
            || string.Equals(entryAssemblyName, "dotnet-getdocument", StringComparison.OrdinalIgnoreCase)
            || Environment.GetCommandLineArgs().Any(arg => arg.Contains("dotnet-getdocument", StringComparison.OrdinalIgnoreCase))
            || AppDomain.CurrentDomain.GetAssemblies().Any(assembly => string.Equals(assembly.GetName().Name, "Microsoft.Extensions.ApiDescription.Tool", StringComparison.Ordinal));
    }


    public static async Task Main(string[] args)
    {
        try
        {
            GlobalInit.InitStatics();

            using var h = GetHostBuilder(args).Build();

            await h.RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            NLog.LogManager.GetLogger("Main").Fatal(ex, global::System.Globalization.CultureInfo.InvariantCulture, "Unhandled Fatal Exception occurred: {Message}", ex.Message);
#pragma warning disable RS0030 // Exception handler - console output for critical failures
            Console.WriteLine(ex.ToString());
#pragma warning restore RS0030
        }
        finally
        {
            NLog.LogManager.GetLogger("Main").Info(global::System.Globalization.CultureInfo.InvariantCulture, "Shutting down");
            NLog.LogManager.Shutdown();
        }
    }
}
