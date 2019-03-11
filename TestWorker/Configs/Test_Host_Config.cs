using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;
using NodaTime;

namespace TestWorker.Configs
{
    public class Test_Host_Config : DefaultHostConfig, ITest_Host_Config
    {
        public Test_Host_Config()
        {
            WorkerName = "TEST";
        }

        public string StateDbConnectionString { get; set; }
        string ISqlStateProviderConfig.DbConnectionString => StateDbConnectionString;
    }
}
