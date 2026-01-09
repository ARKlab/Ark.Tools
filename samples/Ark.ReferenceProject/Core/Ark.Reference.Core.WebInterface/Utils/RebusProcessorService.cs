using Ark.Reference.Core.Application;
using Ark.Reference.Core.Application.Host;

using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using Rebus.Bus;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;

using SimpleInjector;
using SimpleInjector.Lifestyles;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.WebInterface.Utils
{
    public sealed class RebusProcessorService : IHostedService, IDisposable
    {
        private readonly Container _container;

        public RebusProcessorService(IConfiguration config, IServiceProvider services)
        {
            _container = new Container();
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            _container.Register(() => services.GetRequiredService<TelemetryClient>());

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

        }

        public void Dispose()
        {
            _container?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var apiHost = _container.GetInstance<ApiHost>();
            apiHost.RunBusInBackground();

            var bus = _container.GetInstance<IBus>();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_container is not null)
                await _container.DisposeAsync().ConfigureAwait(false);
        }
    }
}