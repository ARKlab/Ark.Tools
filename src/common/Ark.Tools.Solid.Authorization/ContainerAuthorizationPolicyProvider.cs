using Ark.Tools.Authorization;

using System.Collections.Generic;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Solid.Authorization(net10.0)', Before:
namespace Ark.Tools.Solid.Authorization
{
    public class ContainerAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public ContainerAuthorizationPolicyProvider(IEnumerable<IAuthorizationPolicy> policies)
        {
            foreach (var p in policies)
                AddPolicy(p);
        }
=======
namespace Ark.Tools.Solid.Authorization;

public class ContainerAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public ContainerAuthorizationPolicyProvider(IEnumerable<IAuthorizationPolicy> policies)
    {
        foreach (var p in policies)
            AddPolicy(p);
>>>>>>> After


namespace Ark.Tools.Solid.Authorization;

public class ContainerAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public ContainerAuthorizationPolicyProvider(IEnumerable<IAuthorizationPolicy> policies)
    {
        foreach (var p in policies)
            AddPolicy(p);
    }
}