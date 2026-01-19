using Ark.Tools.Authorization;

using SimpleInjector;

using System.Security.Claims;

namespace Ark.Tools.Solid.Authorization;

public class PolicyAuthorizeQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private readonly IAuthorizationService _authSvc;
    private readonly IContextProvider<ClaimsPrincipal> _currentUser;
    private readonly Container _container;
    private readonly IQueryHandler<TQuery, TResult> _inner;
    private readonly PolicyAuthorizeAttribute[] _policies;

    public PolicyAuthorizeQueryDecorator(IQueryHandler<TQuery, TResult> inner, IAuthorizationService authSvc, IContextProvider<ClaimsPrincipal> currentUser, Container container)
    {
        _inner = inner;
        _authSvc = authSvc;
        _currentUser = currentUser;
        _container = container;
        _policies = typeof(TQuery).GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true).OfType<PolicyAuthorizeAttribute>().ToArray();
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Uses dynamic invocation for authorization resource handler dispatch. Handler types must be preserved.")]
    public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
    {
        if (_policies.Length != 0)
        {
            foreach (var p in _policies)
            {
                var policy = await Ex.GetPolicyAsync(p, _authSvc.PolicyProvider, ctk).ConfigureAwait(false);
                var resource = await Ex.GetResourceAsync(_container, query, policy, ctk).ConfigureAwait(false);

                if (policy != null)
                {
                    (var authorized, var messages) = await _authSvc.AuthorizeAsync(_currentUser.Current, resource, policy, ctk).ConfigureAwait(false);
                    if (!authorized)
                        throw new UnauthorizedAccessException($"Security policy {policy.Name} not satisfied, messages: {string.Join(Environment.NewLine, messages)}");
                }
            }
        }

        return await _inner.ExecuteAsync(query, ctk).ConfigureAwait(false);
    }
}