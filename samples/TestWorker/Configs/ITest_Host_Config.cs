using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;

namespace TestWorker.Configs
{
    public interface ITest_Host_Config : IHostConfig, ISqlStateProviderConfig
    {
    }
}