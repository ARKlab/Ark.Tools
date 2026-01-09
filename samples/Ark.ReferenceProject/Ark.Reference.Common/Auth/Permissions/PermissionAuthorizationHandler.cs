using Ark.Tools.Authorization;
using Ark.Tools.Authorization.Requirement;


namespace Ark.Reference.Common.Auth;

public class PermissionAuthorizationHandler<TPermissionEnum> : IAuthorizationHandler
    where TPermissionEnum : System.Enum
{
    private readonly IUserPermissionsProvider<TPermissionEnum> _provider;

    public PermissionAuthorizationHandler(IUserPermissionsProvider<TPermissionEnum> provider)
    {
        _provider = provider;
    }

    public async Task HandleAsync(AuthorizationContext context, CancellationToken ctk = default)
    {
        var permissionType = typeof(PermissionAuthorizationRequirement<TPermissionEnum>);

        var requirements = context.Policy.Requirements.Where(t => permissionType.IsAssignableFrom(t.GetType())).Cast<PermissionAuthorizationRequirement<TPermissionEnum>>().ToArray();
        if (requirements.Length == 0) return;

        var permissions = await _provider.GetPermissions(context).ConfigureAwait(false);
        if (permissions == null || !permissions.Any()) return;

        foreach (var req in requirements)
        {
            if (permissions.Contains(req.Permission))
                context.Succeed(req);
        }
    }
}