using Ark.Tools.Core;
using Ark.Tools.Solid;
using Rebus.Messages;
using Rebus.Pipeline;
using SimpleInjector;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
	[StepDocumentation("Automatically flow UserId and UserEmail from context into a header if possible")]
    public class UserFlowStep : IOutgoingStep, IIncomingStep
    {
        private readonly Container _container;
        private static readonly char[] _separator = new[] { ',' };

        public UserFlowStep(Container container)
        {
            _container = container;
        }

        public async Task Process(OutgoingStepContext context, Func<Task> next)
        {
            var userContext = _container.GetInstance<IContextProvider<ClaimsPrincipal>>();
            if (userContext.Current?.Identity?.IsAuthenticated == true)
            {
                var authType = userContext.Current.Identity.AuthenticationType;
                var userId = userContext.Current.GetUserId();
                var userEmail = userContext.Current.GetUserEmail();
                var scopes = userContext.Current.FindFirst("scope")?.Value;
                var roles = userContext.Current.FindAll(ClaimTypes.Role).Select(x => x.Value) ?? Enumerable.Empty<string>();

                var message = context.Load<Message>();

                if (authType != null)
                {
                    message.Headers["ark-auth-type"] = authType;
                }

                if (userId != null)
                {
                    message.Headers["ark-user-id"] = userId;
                }

                if (userEmail != null)
                {
                    message.Headers["ark-user-email"] = userEmail;
                }

                if (scopes != null)
                {
                    message.Headers["ark-user-scopes"] = scopes;
                }

                if (roles.Any())
                {
                    message.Headers["ark-user-roles"] = string.Join(",", roles);
                }
            }

            await next();
        }

        public async Task Process(IncomingStepContext context, Func<Task> next)
        {
            var message = context.Load<Message>();
            var headers = message.Headers;

            if (headers.TryGetValue("ark-user-id", out var userId))
            {
                var identity = new ClaimsIdentity("SYSTEM");

                if (headers.TryGetValue("ark-auth-type", out var authType))
                    identity = new ClaimsIdentity(authType);

                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));

                if (headers.TryGetValue("ark-user-email", out var email))
                    identity.AddClaim(new Claim(ClaimTypes.Email, email));

                if (headers.TryGetValue("ark-user-scopes", out var scopes))
                    identity.AddClaim(new Claim("scope", scopes));

                if (headers.TryGetValue("ark-user-roles", out var roles))
                    identity.AddClaims(
                        roles.Split(_separator, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => new Claim(ClaimTypes.Role, x)));

                context.Save(new ClaimsPrincipal(new[] { identity }));
            }

            await next();
        }
    }
}
