using Ark.Tools.ApplicationInsights.HostedService;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.ResourceWatcher.ApplicationInsights(net10.0)', Before:
namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
        {
            return builder
                .AddApplicationInsightsForHostedService()
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();
                    services.AddHostedService<StartTelemetryHack>();
                });
        }

        private sealed class StartTelemetryHack : IHostedService
        {
#pragma warning disable IDE0052 // Remove unread private members
            private readonly TelemetryClient _client;
#pragma warning restore IDE0052 // Remove unread private members

            public StartTelemetryHack(TelemetryClient client)
            {
                // only used to 'force' creation of the TelemetryClient which in turn triggers the ResourceWatcherTelemetryModule init and thus the subscription of the Listener.
                _client = client;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
=======
namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

public static partial class Ex
{
    public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
    {
        return builder
            .AddApplicationInsightsForHostedService()
            .ConfigureServices((ctx, services) =>
            {
                services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();
                services.AddHostedService<StartTelemetryHack>();
            });
    }

    private sealed class StartTelemetryHack : IHostedService
    {
#pragma warning disable IDE0052 // Remove unread private members
        private readonly TelemetryClient _client;
#pragma warning restore IDE0052 // Remove unread private members

        public StartTelemetryHack(TelemetryClient client)
        {
            // only used to 'force' creation of the TelemetryClient which in turn triggers the ResourceWatcherTelemetryModule init and thus the subscription of the Listener.
            _client = client;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
>>>>>>> After


namespace Ark.Tools.ResourceWatcher.ApplicationInsights;

    public static partial class Ex
    {
        public static IHostBuilder AddApplicationInsightsForWorkerHost(this IHostBuilder builder)
        {
            return builder
                .AddApplicationInsightsForHostedService()
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSingleton<ITelemetryModule, ResourceWatcherTelemetryModule>();
                    services.AddHostedService<StartTelemetryHack>();
                });
        }

        private sealed class StartTelemetryHack : IHostedService
        {
#pragma warning disable IDE0052 // Remove unread private members
            private readonly TelemetryClient _client;
#pragma warning restore IDE0052 // Remove unread private members

            public StartTelemetryHack(TelemetryClient client)
            {
                // only used to 'force' creation of the TelemetryClient which in turn triggers the ResourceWatcherTelemetryModule init and thus the subscription of the Listener.
                _client = client;
            }

            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }