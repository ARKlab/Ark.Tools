using Ark.Tools.Compliance;
using Ark.Tools.ResourceWatcher;
using Ark.Tools.ResourceWatcher.WorkerHost;

using TestWorker.Constants;

namespace TestWorker.Configs;

public class Test_Host_Config : DefaultHostConfig, ITest_Host_Config
{
#pragma warning disable ARKPII001 // test host config names intentionally model connection-string values and are not user data.
    public override string WorkerName { get; set; } = Test_Constants.AppName;

    [InfrastructureSecret]
    public string? StateDbConnectionString { get; set; }
    string ISqlStateProviderConfig.DbConnectionString => StateDbConnectionString ?? throw new InvalidOperationException("StateDbConnectionString must be configured.");
#pragma warning restore ARKPII001
}