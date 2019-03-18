using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher.ApplicationInsights
{
    internal class ApplicationInsightsStarter : IHostedService
    {
        public ApplicationInsightsStarter(TelemetryConfiguration telemetryConfiguration)
        {

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
