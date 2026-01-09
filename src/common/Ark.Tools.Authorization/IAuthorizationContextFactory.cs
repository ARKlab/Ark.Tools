using System.Security.Claims;

namespace Ark.Tools.Authorization;

/// <summary>
/// A factory used to provide a <see cref="AuthorizationContext"/> used by authorization handlers.
/// Applications with custom handlers can implement a custom factory to provide handlers with additional context informations.
/// </summary>
public interface IAuthorizationContextFactory
{
    /// <summary>
    /// Creates a <see cref="AuthorizationContext"/> used for authorization.
    /// </summary>
    /// <param name="policy">The policy to evaluate.</param>
    /// <param name="user">The user to evaluate the requirements against.</param>
    /// <param name="resource">
    /// An optional resource the policy should be checked with.
    /// If a resource is not required for policy evaluation you may pass null as the value.
    /// </param>
    /// <returns>The <see cref="AuthorizationContext"/>.</returns>
    AuthorizationContext Create(IAuthorizationPolicy policy, ClaimsPrincipal user, object? resource);
}
