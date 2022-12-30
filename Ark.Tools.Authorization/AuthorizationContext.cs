using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Security.Claims;

namespace Ark.Tools.Authorization
{
    /// <summary>
    /// Contains authorization information used by <see cref="IAuthorizationHandler"/>.
    /// </summary>
    public class AuthorizationContext
    {
        private readonly HashSet<IAuthorizationRequirement> _pendingRequirements;
        private readonly HashSet<IAuthorizationRequirement> _failedRequirements;
        private readonly HashSet<IAuthorizationRequirement> _succeededRequirements;
        private bool _failCalled;

        /// <summary>
        /// Creates a new instance of <see cref="AuthorizationContext"/>.
        /// </summary>
        /// <param name="policy">The policy for the current authorization action.</param>
        /// <param name="user">A <see cref="ClaimsPrincipal"/> representing the user under evaluation.</param>
        /// <param name="resource">An optional resource to evaluate the <paramref name="policy"/> against.</param>
        public AuthorizationContext(
            IAuthorizationPolicy policy,
            ClaimsPrincipal user,
            object? resource)
        {
            Policy = policy;
            User = user;
            Resource = resource;

            _pendingRequirements = new HashSet<IAuthorizationRequirement>(policy.Requirements);
            _failedRequirements = new HashSet<IAuthorizationRequirement>();
            _succeededRequirements = new HashSet<IAuthorizationRequirement>();

            Messages = new Dictionary<IAuthorizationRequirement,string>();
        }

        /// <summary>
        /// The policy for the current authorization action.
        /// </summary>
        public virtual IAuthorizationPolicy Policy { get; }

        /// <summary>
        /// The <see cref="ClaimsPrincipal"/> representing the current user.
        /// </summary>
        public virtual ClaimsPrincipal User { get; }

        /// <summary>
        /// The optional resource to evaluate the <see cref="AuthorizationContext.Policy"/> against.
        /// </summary>
        public virtual object? Resource { get; }

        /// <summary>
        /// Gets the requirements that have not yet been marked as succeeded.
        /// </summary>
        public virtual IEnumerable<IAuthorizationRequirement> PendingRequirements { get { return _pendingRequirements; } }

        /// <summary>
        /// Gets the failed requirements.
        /// </summary>
        public virtual IEnumerable<IAuthorizationRequirement> FailedRequirements { get { return _failedRequirements; } }

        /// <summary>
        /// Gets the succeeded requirements.
        /// </summary>
        public virtual IEnumerable<IAuthorizationRequirement> SucceededRequirements { get { return _succeededRequirements; } }

        /// <summary>
        /// Flag indicating whether the current authorization processing has failed.
        /// </summary>
        public virtual bool HasFailed { get { return _failCalled; } }

        /// <summary>
        /// List of messages about failure or success.
        /// </summary>
        public IDictionary<IAuthorizationRequirement, string> Messages { get; private set; }

        /// <summary>
        /// Flag indicating whether the current authorization processing has succeeded.
        /// </summary>
        public virtual bool HasSucceeded
        {
            get
            {
                return !_failCalled && !PendingRequirements.Any() && !_failedRequirements.Any();
            }
        }

        /// <summary>
        /// Called to indicate <see cref="AuthorizationContext.HasSucceeded"/> will
        /// never return true, even if all requirements are met.
        /// </summary>
        public virtual void Fail()
        {
            _failCalled = true;
        }

        /// <summary>
        /// Called to indicate that a requirement is failed with a message
        /// </summary>
        public virtual void Fail(IAuthorizationRequirement requirement, string? message = null)
        {
            if(!string.IsNullOrWhiteSpace(message))
                Messages.Add(requirement, message!);

            _pendingRequirements.Remove(requirement);
            _failedRequirements.Add(requirement);
        }

        /// <summary>
        /// Called to mark the specified <paramref name="requirement"/> as being
        /// successfully evaluated.
        /// </summary>
        /// <param name="requirement">The requirement whose evaluation has succeeded.</param>
        /// <param name="message">Optional: message we want to pass.</param>
        public virtual void Succeed(IAuthorizationRequirement requirement, string? message = null)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Messages.Add(requirement, message!);

            _pendingRequirements.Remove(requirement);
            _succeededRequirements.Add(requirement);
        }
    }
}