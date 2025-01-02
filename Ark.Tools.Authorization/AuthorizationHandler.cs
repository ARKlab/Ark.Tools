using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization
{

    /// <summary>
    /// Base class for authorization handlers that need to be called for a specific requirement type.
    /// </summary>
    /// <typeparam name="TRequirement">The type of the requirement to handle.</typeparam>
    public abstract class AuthorizationHandler<TRequirement> : IAuthorizationHandler
                where TRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Makes a decision if authorization is allowed.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="ctk">CancellationToken</param>
        public virtual async Task HandleAsync(AuthorizationContext context, CancellationToken ctk = default)
        {
            foreach (var req in context.Policy.Requirements.OfType<TRequirement>())
            {
                await HandleRequirementAsync(context, req, ctk).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Makes a decision if authorization is allowed based on a specific requirement.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The requirement to evaluate.</param>
        /// <param name="ctk">CancellationToken</param>
        protected abstract Task HandleRequirementAsync(AuthorizationContext context, TRequirement requirement, CancellationToken ctk = default);
    }

    /// <summary>
    /// Base class for authorization handlers that need to be called for specific requirement and
    /// resource types.
    /// </summary>
    /// <typeparam name="TRequirement">The type of the requirement to evaluate.</typeparam>
    /// <typeparam name="TResource">The type of the resource to evaluate.</typeparam>
    public abstract class AuthorizationHandler<TRequirement, TResource> : IAuthorizationHandler
        where TRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Makes a decision if authorization is allowed.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="ctk">CancellationToken</param>
        public virtual async Task HandleAsync(AuthorizationContext context, CancellationToken ctk = default)
        {
            if (context.Resource is TResource)
            {
                foreach (var req in context.Policy.Requirements.OfType<TRequirement>())
                {
                    await HandleRequirementAsync(context, req, (TResource)context.Resource, ctk).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Makes a decision if authorization is allowed based on a specific requirement and resource.
        /// </summary>
        /// <param name="context">The authorization context.</param>
        /// <param name="requirement">The requirement to evaluate.</param>
        /// <param name="resource">The resource to evaluate.</param>
        /// <param name="ctk">CancellationToken</param>
        protected abstract Task HandleRequirementAsync(AuthorizationContext context, TRequirement requirement, TResource resource, CancellationToken ctk = default);
    }

}
