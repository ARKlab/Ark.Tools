using Ark.Tools.Activity.Provider;
using Ark.Tools.Compliance;

using TestWorker.Constants;

namespace TestWorker.Configs;

public class RebusResourceNotifier_Config : IRebusResourceNotifier_Config
{
#pragma warning disable ARKPII001 // test worker configuration intentionally uses named Azure Service Bus connection-string members.
    public RebusResourceNotifier_Config([InfrastructureSecret] string? asbConnectionString)
    {
        AsbConnectionString = asbConnectionString ?? throw new ArgumentNullException(nameof(asbConnectionString));
    }

    [InfrastructureSecret]
    public string AsbConnectionString { get; set; }
#pragma warning restore ARKPII001
    public string ProviderName { get; set; } = Test_Constants.ProviderName;
    public bool StartAtCreation { get; set; } = Test_Constants.StartAtCreationDefault;

    string IRebusResourceNotifier_Config.AsbConnectionString { get { return this.AsbConnectionString; } }
    string IRebusResourceNotifier_Config.ProviderName { get { return this.ProviderName; } }
    bool IRebusResourceNotifier_Config.StartAtCreation { get { return this.StartAtCreation; } }
}