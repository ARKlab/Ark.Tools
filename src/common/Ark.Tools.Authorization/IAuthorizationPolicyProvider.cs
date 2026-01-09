
namespace Ark.Tools.Authorization;

/// <summary>
/// A type which can provide a <see cref="DefaultAuthorizationPolicyProvider"/> for a particular name.
/// </summary>
public interface IAuthorizationPolicyProvider
{
    /// <summary>
    /// Gets a <see cref="IAuthorizationPolicy"/> from the given <paramref name="policyName"/>
    /// </summary>
    /// <param name="policyName">The policy name to retrieve.</param>
    /// <param name="ctk">CancellationToken</param>
    /// <returns>The named <see cref="IAuthorizationPolicy"/>.</returns>
    Task<IAuthorizationPolicy?> GetPolicyAsync(string policyName, CancellationToken ctk = default);
}