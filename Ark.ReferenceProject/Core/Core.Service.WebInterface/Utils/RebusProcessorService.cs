using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ApplicationInsights;
using Core.Service.Application.Host;
using Core.Service.Application;
using Rebus.Bus;
using NodaTime;

namespace Core.Service.WebInterface.Utils
{
    public sealed class RebusProcessorService : IHostedService, IDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Ark.Tools bug")]
        private readonly Container _container;

        public RebusProcessorService(IConfiguration config, IServiceProvider services)
        {
            _container = new Container();
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            _container.Register(() => services.GetService<TelemetryClient>());

            var cfg = config.BuildApiHostConfig();

            new ApiHost(cfg)
                .WithContainer(_container)
                .WithIClock(services.GetService<IClock>())
                .WithRebus(Queue.Main,
                    services.GetService<InMemNetwork>(),
                    services.GetService<InMemorySubscriberStore>()
                    )
                .WithRebusIdentity()
                ;
            ;
        }

        public void Dispose()
        {
            // BUG: Ark.Tools.Outbox.Rebus has a bug in Disposing in which it hangs
            // _container?.Dispose();
        }

        public async Task StartAsync(CancellationToken ctk = default)
        {
            var apiHost = _container.GetInstance<ApiHost>();
            apiHost.RunBusInBackground();

            var bus = _container.GetInstance<IBus>();
        }

        public Task StopAsync(CancellationToken ctk = default)
        {
            _container?.Dispose();
            return Task.CompletedTask;
        }
    }
}