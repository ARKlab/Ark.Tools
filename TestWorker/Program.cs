using Ark.Tools.ResourceWatcher.WorkerHost.Hosting;
using TestWorker.Host;

namespace TestWorker
{
    class Program
    {
        static void Main(string[] args) =>

        Test_Host
            .ConfigureFromAppSettings()
            .StartAndWaitForShutdown();
    }
}
