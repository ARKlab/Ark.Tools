
namespace Ark.Tools.Authorization;


public abstract class AuthorizationPolicy : IAuthorizationPolicy
{
    private readonly Lazy<IAuthorizationPolicy> _inner;

    protected AuthorizationPolicy()
    {
        _inner = new Lazy<IAuthorizationPolicy>(_build, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    protected abstract void Build(AuthorizationPolicyBuilder builder);

    public IReadOnlyList<IAuthorizationRequirement> Requirements => _inner.Value.Requirements;

    public string Name => _inner.Value.Name;

    private IAuthorizationPolicy _build()
    {
        var builder = new AuthorizationPolicyBuilder(GetType().FullName ?? GetType().Name);
        Build(builder);
        return builder.Build();
    }
}