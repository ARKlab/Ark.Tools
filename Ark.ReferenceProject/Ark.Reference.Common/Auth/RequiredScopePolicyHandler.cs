using Ark.Tools.Authorization;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Auth
{
    public abstract class RequiredScopePolicyHandler : AuthorizationHandler<RequiredScopePolicy>
    {
        private readonly string _serviceScope;
        private static readonly char[] _separator = new[] { ' ' };

        protected RequiredScopePolicyHandler(
            string serviceScope)
        {
            _serviceScope = serviceScope;
        }

        protected override Task HandleRequirementAsync(AuthorizationContext context, RequiredScopePolicy requirement, CancellationToken ctk = default)
        {
            var scopes = context.User
                .FindAll(x => x.Type == _serviceScope)
                .SelectMany(x => x.Value.Split(_separator, StringSplitOptions.RemoveEmptyEntries))
                .ToList();

            var requiredScope = requirement.Scope;

            if (!scopes.Contains(requiredScope))
                context.Fail(requirement, $"User does not possess the required scopes '{requiredScope}'");
            else
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
