﻿using Ark.Tools.Authorization;

using SimpleInjector;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Solid.Authorization
{
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

        public TResult Execute(TQuery query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<TResult> ExecuteAsync(TQuery query, CancellationToken ctk = default)
        {
            if (_policies.Any())
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
}