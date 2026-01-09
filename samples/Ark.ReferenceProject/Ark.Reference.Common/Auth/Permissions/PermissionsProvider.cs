using Ark.Tools.Authorization;
using Ark.Tools.Authorization.Requirement;


namespace Ark.Reference.Common.Auth;

public class PermissionsProvider : IUserPermissionsProvider<Permissions>
{
    public async Task<IEnumerable<Permissions>> GetPermissions(AuthorizationContext context)
    {
        return context.User.GetPermissions();
    }
}