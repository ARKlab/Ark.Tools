using System;
using System.Linq;
using System.Threading.Tasks;
using Ark.Tools.Authorization;
using System.Threading;
using SimpleInjector;
using System.Security.Claims;

namespace Ark.Tools.Solid.Authorization
{
    public class PolicyAuthorizeCommandDecorator<TCommand> : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        private readonly IAuthorizationService _authSvc;
        private readonly IContextProvider<ClaimsPrincipal> _currentUser;
        private readonly Container _container;
        private readonly ICommandHandler<TCommand> _inner;
        private readonly PolicyAuthorizeAttribute[] _policies;

        public PolicyAuthorizeCommandDecorator(ICommandHandler<TCommand> inner, IAuthorizationService authSvc, IContextProvider<ClaimsPrincipal> currentUser, Container container)
        {
            _inner = inner;
            _authSvc = authSvc;
            _currentUser = currentUser;
            _container = container;
            _policies = typeof(TCommand).GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true).Select(a => (a as PolicyAuthorizeAttribute)).ToArray();
        }

        public void Execute(TCommand command)
        {
            ExecuteAsync(command).GetAwaiter().GetResult();
        }

        public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default(CancellationToken))
        {
            if (_policies.Any())
            {
                foreach (var p in _policies)
                {
                    var policy = await Ex.GetPolicyAsync(p, _authSvc.PolicyProvider);
                    var resource = await Ex.GetResourceAsync(_container, command, policy);

                    if (policy != null)
                    {
                        (var authorized, var messages) = await _authSvc.AuthorizeAsync(_currentUser.Current, resource, policy);
                        if (!authorized)
                            throw new UnauthorizedAccessException($"Security policy {policy.Name} not satisfied, messages: {string.Join(Environment.NewLine, messages)}");
                    }
                }
            }

            await _inner.ExecuteAsync(command, ctk);
        }
    }
}