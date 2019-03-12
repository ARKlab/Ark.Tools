using Ark.Tools.ResourceWatcher.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Threading;
using TestWorker.Host;

namespace TestWorker
{
    class Program
    {
        static void Main(string[] args)
        {

            var cfg = TelemetryConfiguration.CreateDefault();
            var listener = new ResourceWatcherDiagnosticListener(cfg);

            Test_Host
                .ConfigureFromAppSettings()
                .Start();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
