using System;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.Authorization;
using System.Threading;
using SimpleInjector;
using System.Security.Claims;

namespace Ark.Tools.Solid.Authorization
{
    public class PolicyAuthorizeRequestDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>
        where TRequest : IRequest<TResult>
    {
        private readonly IAuthorizationService _authSvc;
        private readonly IContextProvider<ClaimsPrincipal> _currentUser;
        private readonly Container _container;
        private readonly IRequestHandler<TRequest, TResult> _inner;
        private readonly PolicyAuthorizeAttribute[] _policies;

        public PolicyAuthorizeRequestDecorator(IRequestHandler<TRequest, TResult> inner, IAuthorizationService authSvc, IContextProvider<ClaimsPrincipal> currentUser, Container container)
        {
            _inner = inner;
            _authSvc = authSvc;
            _currentUser = currentUser;
            _container = container;
            _policies = typeof(TRequest).GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true).OfType<PolicyAuthorizeAttribute>().ToArray();
        }

        public TResult Execute(TRequest request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        public async Task<TResult> ExecuteAsync(TRequest request, CancellationToken ctk = default)
        {
            if (_policies.Any())
            {
                foreach (var p in _policies)
                {
                    var policy = await Ex.GetPolicyAsync(p, _authSvc.PolicyProvider, ctk);
                    var resource = await Ex.GetResourceAsync(_container, request, policy, ctk);

                    if (policy != null)
                    {
                        (var authorized, var messages) = await _authSvc.AuthorizeAsync(_currentUser.Current ?? new ClaimsPrincipal(), resource, policy, ctk);
                        if (!authorized)
                            throw new UnauthorizedAccessException($"Security policy {policy.Name} not satisfied, messages: {string.Join(Environment.NewLine, messages)}");
                    }
                }
            }

            return await _inner.ExecuteAsync(request, ctk);
        }
    }
}