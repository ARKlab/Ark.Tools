using Ark.Tools.Authorization;
using Ark.Tools.Solid;

using SimpleInjector;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;


namespace Ark.Reference.Common.Services.Auth
{
    public class PolicyAuthorizeOrLogicCommandDecorator<TCommand> : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        private readonly IAuthorizationService _authSvc;
        private readonly IContextProvider<ClaimsPrincipal> _currentUser;
        private readonly Container _container;
        private readonly ICommandHandler<TCommand> _inner;
        private readonly PolicyAuthorizeAttribute[] _policies;

        public PolicyAuthorizeOrLogicCommandDecorator(ICommandHandler<TCommand> inner, IAuthorizationService authSvc, IContextProvider<ClaimsPrincipal> currentUser, Container container)
        {
            _inner = inner;
            _authSvc = authSvc;
            _currentUser = currentUser;
            _container = container;
            _policies = typeof(TCommand).GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true).Cast<PolicyAuthorizeAttribute>().ToArray();
        }

        public void Execute(TCommand command)
        {
            ExecuteAsync(command).GetAwaiter().GetResult();
        }

        public async Task ExecuteAsync(TCommand command, CancellationToken ctk = default)
        {
            if (_policies.Length != 0)
            {
                var policyFailed = new Dictionary<string, List<string>>(StringComparer.Ordinal);

                foreach (var p in _policies)
                {
                    var policy = await Ark.Tools.Solid.Authorization.Ex.GetPolicyAsync(p, _authSvc.PolicyProvider, ctk);
                    var resource = await Ark.Tools.Solid.Authorization.Ex.GetResourceAsync(_container, command, policy, ctk);

                    if (policy != null)
                    {
                        (var authorized, var messages) = await _authSvc.AuthorizeAsync(_currentUser.Current, resource, policy, ctk);

                        if (authorized)
                        {
                            await _inner.ExecuteAsync(command, ctk);
                            return;
                        }
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

            await _inner.ExecuteAsync(command, ctk);
        }
    }
}