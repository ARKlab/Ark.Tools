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
using Ark.Reference.Core.Application.Host;
using Ark.Reference.Core.Application;
using Rebus.Bus;
using NodaTime;

namespace Ark.Reference.Core.WebInterface.Utils
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
                .WithRebus(Queue.Core,
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

        public Task StartAsync(CancellationToken ctk = default)
        {
            var apiHost = _container.GetInstance<ApiHost>();
            apiHost.RunBusInBackground();

            var bus = _container.GetInstance<IBus>();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ctk = default)
        {
            _container?.Dispose();
            return Task.CompletedTask;
        }
    }
}