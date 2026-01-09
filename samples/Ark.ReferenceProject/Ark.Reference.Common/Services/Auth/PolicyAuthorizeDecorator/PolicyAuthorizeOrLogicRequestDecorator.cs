using Ark.Tools.Authorization;
using Ark.Tools.Solid;

using SimpleInjector;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;


namespace Ark.Reference.Common.Services.Auth;

public class PolicyAuthorizeOrLogicRequestDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    private readonly IAuthorizationService _authSvc;
    private readonly IContextProvider<ClaimsPrincipal> _currentUser;
    private readonly Container _container;
    private readonly IRequestHandler<TRequest, TResult> _inner;
    private readonly PolicyAuthorizeAttribute[] _policies;

    public PolicyAuthorizeOrLogicRequestDecorator(IRequestHandler<TRequest, TResult> inner, IAuthorizationService authSvc, IContextProvider<ClaimsPrincipal> currentUser, Container container)
    {
        _inner = inner;
        _authSvc = authSvc;
        _currentUser = currentUser;
        _container = container;
        _policies = typeof(TRequest).GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true).Cast<PolicyAuthorizeAttribute>().ToArray();
    }

    public TResult Execute(TRequest request)
    {
        return ExecuteAsync(request).GetAwaiter().GetResult();
    }

    public async Task<TResult> ExecuteAsync(TRequest request, CancellationToken ctk = default)
    {
        if (_policies.Length != 0)
        {
            var policyFailed = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (var p in _policies)
            {
                var policy = await Tools.Solid.Authorization.Ex.GetPolicyAsync(p, _authSvc.PolicyProvider, ctk).ConfigureAwait(false);
                var resource = await Tools.Solid.Authorization.Ex.GetResourceAsync(_container, request, policy, ctk).ConfigureAwait(false);

                if (policy != null)
                {
                    (var authorized, var messages) = await _authSvc.AuthorizeAsync(_currentUser.Current, resource, policy, ctk).ConfigureAwait(false);

                    if (authorized)
                        return await _inner.ExecuteAsync(request, ctk).ConfigureAwait(false);
                    else
                        policyFailed.Add(policy.Name, messages.ToList());
                }
            }

            if (policyFailed.Count > 0)
            {
                var exceptionMessage = new List<string>();
                foreach (var pFail in policyFailed)
                    exceptionMessage.Add($"Security policy {pFail.Key} not satisfied, messages: {string.Join(Environment.NewLine, pFail.Value)}");

                throw new UnauthorizedAccessException(string.Join(Environment.NewLine, exceptionMessage));
            }
        }

        return await _inner.ExecuteAsync(request, ctk).ConfigureAwait(false);
    }
}