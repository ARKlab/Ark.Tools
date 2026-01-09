using Ark.Tools.Authorization;

using System.Collections.Generic;

namespace Ark.Tools.Solid.Authorization;

public class ContainerAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public ContainerAuthorizationPolicyProvider(IEnumerable<IAuthorizationPolicy> policies)
    {
        foreach (var p in policies)
            AddPolicy(p);
    }
}
