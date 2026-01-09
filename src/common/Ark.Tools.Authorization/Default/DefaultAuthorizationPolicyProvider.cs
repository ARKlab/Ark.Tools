
namespace Ark.Tools.Authorization;

/// <summary>
/// The default implementation of a policy provider,
/// which provides a <see cref="IAuthorizationPolicy"/> for a particular name.
/// </summary>
public class DefaultAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly IDictionary<string, IAuthorizationPolicy> _policyMap = new Dictionary<string, IAuthorizationPolicy>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Add an authorization policy with the provided name.
    /// </summary>
    /// <param name="policy">The authorization policy.</param>
    public void AddPolicy(IAuthorizationPolicy policy)
    {
        if (policy == null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        _policyMap.Add(policy.Name, policy);
    }

    /// <summary>
    /// Add a policy that is built from a delegate with the provided name.
    /// </summary>
    /// <param name="name">The name of the policy.</param>
    /// <param name="configurePolicy">The delegate that will be used to build the policy.</param>
    public void AddPolicy(string name, Action<AuthorizationPolicyBuilder> configurePolicy)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (configurePolicy == null)
        {
            throw new ArgumentNullException(nameof(configurePolicy));
        }

        var policyBuilder = new AuthorizationPolicyBuilder(name);
        configurePolicy(policyBuilder);
        AddPolicy(policyBuilder.Build());
    }

    /// <summary>
    /// Gets a <see cref="IAuthorizationPolicy"/> from the given <paramref name="policyName"/>
    /// </summary>
    /// <param name="policyName">The policy name to retrieve.</param>
    /// <param name="ctk">CancellationToken</param>
    /// <returns>The named <see cref="IAuthorizationPolicy"/> or null.</returns>
    public virtual Task<IAuthorizationPolicy?> GetPolicyAsync(string policyName, CancellationToken ctk = default)
    {
        // MVC caches policies specifically for this class, so this method MUST return the same policy per
        // policyName for every request or it could allow undesired access. It also must return synchronously.
        // A change to either of these behaviors would require shipping a patch of MVC as well.
        return Task.FromResult(_policyMap.TryGetValue(policyName, out IAuthorizationPolicy? value) ? value : null);
    }
}