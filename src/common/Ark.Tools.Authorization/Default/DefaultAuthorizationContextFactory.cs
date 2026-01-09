using System.Security.Claims;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Authorization(net10.0)', Before:
namespace Ark.Tools.Authorization
{
    public class DefaultAuthorizationContextFactory : IAuthorizationContextFactory
    {
        public AuthorizationContext Create(IAuthorizationPolicy policy, ClaimsPrincipal user, object? resource)
        {
            return new AuthorizationContext(policy, user, resource);
        }
=======
namespace Ark.Tools.Authorization;

public class DefaultAuthorizationContextFactory : IAuthorizationContextFactory
{
    public AuthorizationContext Create(IAuthorizationPolicy policy, ClaimsPrincipal user, object? resource)
    {
        return new AuthorizationContext(policy, user, resource);
>>>>>>> After


namespace Ark.Tools.Authorization;

    public class DefaultAuthorizationContextFactory : IAuthorizationContextFactory
    {
        public AuthorizationContext Create(IAuthorizationPolicy policy, ClaimsPrincipal user, object? resource)
        {
            return new AuthorizationContext(policy, user, resource);
        }
    }