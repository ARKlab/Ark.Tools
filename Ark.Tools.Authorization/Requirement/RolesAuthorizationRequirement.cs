using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization.Requirement
{
    /// <summary>
    /// Implements an <see cref="IAuthorizationHandler"/> and <see cref="IAuthorizationRequirement"/>
    /// which requires at least one role claim whose value must be any of the allowed roles.
    /// </summary>
    public class RolesAuthorizationRequirement : AuthorizationHandler<RolesAuthorizationRequirement>, IAuthorizationRequirement
    {
        /// <summary>
        /// Creates a new instance of <see cref="RolesAuthorizationRequirement"/>.
        /// </summary>
        /// <param name="allowedRoles">A collection of allowed roles.</param>
        public RolesAuthorizationRequirement(IEnumerable<string> allowedRoles)
        {
            AllowedRoles = allowedRoles;
        }

        /// <summary>
        /// Gets the collection of allowed roles.
        /// </summary>
        public IEnumerable<string> AllowedRoles { get; }

        /// <summary>
        /// Makes a decision if authorization is allowed based on a specific requirement.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The requirement to evaluate.</param>
        /// <param name="ctk">CancellationToken</param>

        protected override Task HandleRequirementAsync(AuthorizationContext context, RolesAuthorizationRequirement requirement, CancellationToken ctk = default)
        {
            if (context.User != null)
            {
                bool found = requirement.AllowedRoles.Any(r => context.User.IsInRole(r));
                
                if (found)
                {
                    context.Succeed(requirement);
                }
            }
            return Task.CompletedTask;
        }

    }
}
