using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Authorization.Requirement;

/// <summary>
/// Implements an <see cref="IAuthorizationHandler"/> and <see cref="IAuthorizationRequirement"/>
/// which requires the current user must be authenticated.
/// </summary>
public class DenyAnonymousAuthorizationRequirement : AuthorizationHandler<DenyAnonymousAuthorizationRequirement>, IAuthorizationRequirement
{
    /// <summary>
    /// Makes a decision if authorization is allowed based on a specific requirement.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The requirement to evaluate.</param>
    /// <param name="ctk">CancellationToken</param>
    protected override Task HandleRequirementAsync(AuthorizationContext context, DenyAnonymousAuthorizationRequirement requirement, CancellationToken ctk = default)
    {
        var user = context.User;
        var userIsAnonymous =
            user?.Identity == null ||
            !user.Identities.Any(i => i.IsAuthenticated);
        if (!userIsAnonymous)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
