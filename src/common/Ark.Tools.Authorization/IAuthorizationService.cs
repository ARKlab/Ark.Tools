using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization
{
    /// <summary>
    /// Checks policy based permissions for a user
    /// </summary>
    public interface IAuthorizationService
    {
        IAuthorizationPolicyProvider PolicyProvider { get; }


        /// <summary>
        /// Checks if a user meets a specific authorization policy
        /// </summary>
        /// <param name="user">The user to check the policy against.</param>
        /// <param name="resource">
        /// An optional resource the policy should be checked with.
        /// If a resource is not required for policy evaluation you may pass null as the value.
        /// </param>
        /// <param name="policyName">The name of the policy to check against a specific context.</param>
        /// <param name="ctk">CancellationToken</param>
        /// <returns>
        /// A flag indicating whether authorization has succeeded.
        /// Returns a flag indicating whether the user, and optional resource has fulfilled the policy.    
        /// <value>true</value> when the the policy has been fulfilled; otherwise <value>false</value>.
        /// </returns>
        /// <remarks>
        /// Resource is an optional parameter and may be null. Please ensure that you check it is not 
        /// null before acting upon it.
        /// </remarks>
        Task<(bool, IList<string>)> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName, CancellationToken ctk = default);

        /// <summary>
        /// Checks if a user meets a specific authorization policy
        /// </summary>
        /// <param name="user">The user to check the policy against.</param>
        /// <param name="resource">
        /// An optional resource the policy should be checked with.
        /// If a resource is not required for policy evaluation you may pass null as the value.
        /// </param>
        /// <param name="policy">The policy to check against a specific context.</param>
        /// <param name="ctk">CancellationToken</param>
        /// <returns>
        /// A flag indicating whether authorization has succeeded.
        /// Returns a flag indicating whether the user, and optional resource has fulfilled the policy.    
        /// <value>true</value> when the the policy has been fulfilled; otherwise <value>false</value>.
        /// </returns>
        /// <remarks>
        /// Resource is an optional parameter and may be null. Please ensure that you check it is not 
        /// null before acting upon it.
        /// </remarks>
        Task<(bool, IList<string>)> AuthorizeAsync(ClaimsPrincipal user, object? resource, IAuthorizationPolicy policy, CancellationToken ctk = default);
    }
}