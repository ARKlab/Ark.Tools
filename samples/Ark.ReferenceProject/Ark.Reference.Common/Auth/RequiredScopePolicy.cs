using Ark.Tools.Authorization;

namespace Ark.Reference.Common.Auth;

public class RequiredScopePolicy : AuthorizationPolicy, IAuthorizationRequirement
{
    public string Scope { get; set; }

    public RequiredScopePolicy(string scope)
    {
        Scope = scope;
    }

    protected override void Build(AuthorizationPolicyBuilder builder)
    {
        builder.AddRequirements(this);
    }
}