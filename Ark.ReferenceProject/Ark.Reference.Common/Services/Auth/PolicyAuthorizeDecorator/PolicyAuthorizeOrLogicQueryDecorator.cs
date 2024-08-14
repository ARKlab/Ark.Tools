using System;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.Authorization;
using System.Threading;
using SimpleInjector;
using System.Security.Claims;
using System.Collections.Generic;
using Ark.Tools.Solid;


namespace Ark.Reference.Common.Services.Auth
{
    public class PolicyAuthorizeOrLogicQueryDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        private readonly IAuthorizationService _authSvc;
        private readonly IContextProvider<ClaimsPrincipal> _currentUser;
        private readonly Container _container;
        private readonly IQueryHandler<TQuery, TResult> _inner;
        private readonly PolicyAuthorizeAttribute[] _policies;

        public PolicyAuthorizeOrLogicQueryDecorator(IQueryHandler<TQuery, TResult> inner, IAuthorizationService authSvc, IContextProvider<ClaimsPrincipal> currentUser, Container container)
        {
            _inner = inner;
            _authSvc = authSvc;
            _currentUser = currentUser;
            _container = container;
            _policies = typeof(TQuery).GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true).Cast<PolicyAuthorizeAttribute>().ToArray();
        }

        public TResult Execute(TQuery query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            if (_policies.Length != 0)
            {
                var policyFailed = new Dictionary<string, List<string>>();

                foreach (var p in _policies)
                {
                    var policy = await Ark.Tools.Solid.Authorization.Ex.GetPolicyAsync(p, _authSvc.PolicyProvider, ctk);
                    var resource = await Ark.Tools.Solid.Authorization.Ex.GetResourceAsync(_container, query, policy, ctk);

                    if (policy != null)
                    {
                            (var authorized, var messages) = await _authSvc.AuthorizeAsync(_currentUser.Current, resource, policy, ctk);

                            if (authorized)
                                return await _inner.ExecuteAsync(query, ctk);
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

            return await _inner.ExecuteAsync(query, ctk);
        }
    }
}