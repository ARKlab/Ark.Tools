#if NET10_0_OR_GREATER
using Ark.Tools.Compliance;
#endif

namespace Ark.Tools.Activity.Provider;

public interface IRebusResourceNotifier_Config
{
    string ProviderName { get; }
    #if NET10_0_OR_GREATER
    [Secret]
    #endif
    string AsbConnectionString { get; }
    bool StartAtCreation { get; }
}