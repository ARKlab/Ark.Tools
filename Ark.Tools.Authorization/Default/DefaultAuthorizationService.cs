using NLog;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization
{
    /// <summary>
    /// The default implementation of an <see cref="IAuthorizationService"/>.
    /// </summary>
    public class DefaultAuthorizationService : IAuthorizationService
    {
        private static ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly IAuthorizationContextFactory _contextFactory;
        private readonly IAuthorizationPolicyProvider _policyProvider;
        private readonly IEnumerable<IAuthorizationHandler> _handlers;
        private readonly IAuthorizationContextEvaluator _evaluator;

        public IAuthorizationPolicyProvider PolicyProvider
        {
            get
            {
                return _policyProvider;
            }
        }

        /// <summary>
        /// Creates a new instance of <see cref="DefaultAuthorizationService"/>.
        /// </summary>
        /// <param name="policyProvider">The <see cref="IAuthorizationPolicyProvider"/> used to provide policies.</param>
        /// <param name="contextFactory">The <see cref="IAuthorizationContextFactory"/> used to initialize the <see cref="AuthorizationContext"/> used by <see cref="IAuthorizationHandler"/>.</param>  
        /// <param name="evaluator">The <see cref="IAuthorizationContextEvaluator"/> used to determine if authorzation was successful.</param>
        /// <param name="handlers">The <see cref="IAuthorizationHandler"/> used to handle policies' requirements.</param>
        public DefaultAuthorizationService(
            IAuthorizationPolicyProvider policyProvider,
            IAuthorizationContextFactory contextFactory,
            IAuthorizationContextEvaluator evaluator,
            IEnumerable<IAuthorizationHandler> handlers)
        {
            if (policyProvider == null) throw new ArgumentNullException(nameof(policyProvider));
            if (contextFactory == null) throw new ArgumentNullException(nameof(contextFactory));
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));
            if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));

            _policyProvider = policyProvider;
            _handlers = handlers;
            _contextFactory = contextFactory;
            _evaluator = evaluator;
        }


        private string _getUserNameForLogging(ClaimsPrincipal user)
        {
            var identity = user?.Identity;
            if (identity != null)
            {
                var name = identity.Name;
                if (name != null)
                {
                    return name;
                }
                return _getClaimValue(identity, "sub")
                    ?? _getClaimValue(identity, ClaimTypes.Name)
                    ?? _getClaimValue(identity, ClaimTypes.NameIdentifier);
            }
            return null;
        }

        private static string _getClaimValue(IIdentity identity, string claimsType)
        {
            return (identity as ClaimsIdentity)?.FindFirst(claimsType)?.Value;
        }

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
        public virtual async Task<(bool, IList<string>)> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName, CancellationToken ctk = default)
        {
            if (policyName == null)
            {
                throw new ArgumentNullException(nameof(policyName));
            }

            var policy = await this.PolicyProvider.GetPolicyAsync(policyName, ctk);
            if (policy == null) throw new InvalidOperationException($"No policy found: {policyName}.");

            return await AuthorizeAsync(user, resource, policy, ctk);
        }

        /// <summary>
        /// Checks if a user meets a specific authorization policy
        /// </summary>
        /// <param name="user">The user to check the policy against.</param>
        /// <param name="resource">
        /// An optional resource the policy should be checked with.
        /// If a resource is not required for policy evaluation you may pass null as the value.
        /// </param>
        /// <param name="policy">The name of the policy to check against a specific context.</param>
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
        public virtual async Task<(bool, IList<string>)> AuthorizeAsync(ClaimsPrincipal user, object resource, IAuthorizationPolicy policy, CancellationToken ctk = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            var authContext = _contextFactory.Create(policy, user, resource);
            foreach (var handler in _handlers)
            {
                await handler.HandleAsync(authContext, ctk);
            }

            (var authorized, var messages) = _evaluator.Evaluate(authContext);

            if (authorized)
            {
                _logger.UserAuthorizationSucceeded(_getUserNameForLogging(user), policy.Name);
                return (authorized, messages);
            }
            else
            {
                _logger.UserAuthorizationFailed(_getUserNameForLogging(user), policy.Name, authContext.PendingRequirements);
                return (authorized, messages);
            }
        }
    }
}
