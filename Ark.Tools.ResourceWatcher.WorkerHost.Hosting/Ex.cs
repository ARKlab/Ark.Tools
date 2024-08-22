using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Ark.Tools.ResourceWatcher.WorkerHost.Hosting
{
    public static partial class Ex
    {
        public static IHostBuilder AddWorkerHostInfrastracture(this IHostBuilder builder)
        {
            return builder.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddArkEnvironmentVariables();
            });
        }

        public static IHostBuilder AddWorkerHost<T>(this IHostBuilder builder, Func<IServiceProvider, T> configHost) where T : WorkerHost
        {
            return builder.ConfigureServices(services =>
            {
                services.AddSingleton(configHost);

                services.AddSingleton<IHostedService, HostServiceWrap<T>>();
            });
        }

        public static void StartAndWaitForShutdown(this IHostBuilder builder)
        {
            var host = builder.Build();

            using (host)
            {
                host.Start();
                host.WaitForShutdown();
            }
        }
    }

    class HostServiceWrap<T> : IHostedService where T : WorkerHost
    {
        private readonly T _host;

        public HostServiceWrap(T host)
        {
            _host = host;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => _host.Start(), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => _host.Stop(), cancellationToken);
        }
    }
}
