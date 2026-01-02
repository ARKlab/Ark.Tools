using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization
{
    /// <summary>
    /// Extension methods for <see cref="IAuthorizationService"/>.
    /// </summary>
    public static class AuthorizationServiceExtensions
    {

        /// <summary>
        /// Checks if a user meets a specific authorization policy.
        /// </summary>
        /// <param name="service">The <see cref="IAuthorizationService"/> providing authorization.</param>
        /// <param name="user">The user to evaluate the policy against.</param>
        /// <param name="policyName">The name of the policy to evaluate.</param>
        /// <param name="ctk">CancellationToken</param>
        /// <returns>
        /// A flag indicating whether policy evaluation has succeeded or failed.
        /// This value is <value>true</value> when the user fulfills the policy, otherwise <value>false</value>.
        /// </returns>
        public static Task<(bool, IList<string>)> AuthorizeAsync(this IAuthorizationService service, ClaimsPrincipal user, string policyName, CancellationToken ctk = default)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (policyName == null)
            {
                throw new ArgumentNullException(nameof(policyName));
            }

            return service.AuthorizeAsync(user, resource: null, policyName: policyName, ctk: ctk);
        }

        /// <summary>
        /// Checks if a user meets a specific authorization policy.
        /// </summary>
        /// <param name="service">The <see cref="IAuthorizationService"/> providing authorization.</param>
        /// <param name="user">The user to evaluate the policy against.</param>
        /// <param name="policy">The policy to evaluate.</param>
        /// <param name="ctk">CancellationToken</param>
        /// <returns>
        /// A flag indicating whether policy evaluation has succeeded or failed.
        /// This value is <value>true</value> when the user fulfills the policy, otherwise <value>false</value>.
        /// </returns>
        public static Task<(bool, IList<string>)> AuthorizeAsync(this IAuthorizationService service, ClaimsPrincipal user, IAuthorizationPolicy policy, CancellationToken ctk = default)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (policy == null)
            {
                throw new ArgumentNullException(nameof(policy));
            }

            return service.AuthorizeAsync(user, resource: null, policy: policy, ctk: ctk);
        }

        /// <summary>
        /// Checks if a user meets a specific authorization policy.
        /// </summary>
        /// <param name="service">The <see cref="IAuthorizationService"/> providing authorization.</param>
        /// <param name="user">The user to evaluate the policy against.</param>
        /// <typeparam name="TPolicy">The policy to evaluate</typeparam>
        /// <param name="ctk">CancellationToken</param>
        /// <returns>
        /// A flag indicating whether policy evaluation has succeeded or failed.
        /// This value is <value>true</value> when the user fulfills the policy, otherwise <value>false</value>.
        /// </returns>
        public static Task<(bool, IList<string>)> AuthorizeAsync<TPolicy>(this IAuthorizationService service, ClaimsPrincipal user, CancellationToken ctk = default)
            where TPolicy : class, IAuthorizationPolicy, new()
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            return service.AuthorizeAsync(user, resource: null, policy: new TPolicy(), ctk: ctk);
        }

        /// <summary>
        /// Checks if a user meets a specific authorization policy against a given resource.
        /// </summary>
        /// <param name="service">The <see cref="IAuthorizationService"/> providing authorization.</param>
        /// <param name="user">The user to evaluate the policy against.</param>
        /// <param name="resource">The resource to evaluate against.</param>
        /// <typeparam name="TPolicy">The policy to evaluate</typeparam>
        /// <param name="ctk">CancellationToken</param>
        /// <returns>
        /// A flag indicating whether policy evaluation has succeeded or failed.
        /// This value is <value>true</value> when the user fulfills the policy, otherwise <value>false</value>.
        /// </returns>
        public static Task<(bool, IList<string>)> AuthorizeAsync<TPolicy>(this IAuthorizationService service, ClaimsPrincipal user, object resource, CancellationToken ctk = default)
            where TPolicy : class, IAuthorizationPolicy, new()
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            return service.AuthorizeAsync(user, resource: resource, policy: new TPolicy(), ctk: ctk);
        }
    }
}