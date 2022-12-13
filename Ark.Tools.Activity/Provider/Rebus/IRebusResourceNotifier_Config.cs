
namespace Ark.Tools.Activity.Provider
{
    public interface IRebusResourceNotifier_Config
    {
        string ProviderName { get; }
        string AsbConnectionString { get; }
        bool StartAtCreation { get; }
    }
}
