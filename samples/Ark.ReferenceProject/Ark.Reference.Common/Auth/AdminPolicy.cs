using Ark.Tools.Authorization;
using Ark.Tools.Authorization.Requirement;


namespace Ark.Reference.Common.Auth
{
    public class AdminPolicy : AuthorizationPolicy
    {
        protected override void Build(AuthorizationPolicyBuilder builder)
        {
            builder.RequireUserPermission(Permissions.Admin);
        }
    }
}
