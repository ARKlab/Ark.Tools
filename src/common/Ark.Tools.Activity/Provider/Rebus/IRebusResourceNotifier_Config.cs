using Ark.Tools.Compliance;

namespace Ark.Tools.Activity.Provider;

public interface IRebusResourceNotifier_Config
{
    string ProviderName { get; }
    [InfrastructureSecret]
    string AsbConnectionString { get; }
    bool StartAtCreation { get; }
}