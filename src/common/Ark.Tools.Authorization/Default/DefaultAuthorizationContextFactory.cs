using System.Security.Claims;

namespace Ark.Tools.Authorization
{
    public class DefaultAuthorizationContextFactory : IAuthorizationContextFactory
    {
        public AuthorizationContext Create(IAuthorizationPolicy policy, ClaimsPrincipal user, object? resource)
        {
            return new AuthorizationContext(policy, user, resource);
        }
    }
}