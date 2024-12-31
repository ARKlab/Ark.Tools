using Ark.Tools.Solid;

using System.Security.Claims;


namespace Ark.Tools.Rebus
{
    public class RebusPrincipalContextWithFallbackProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly IMessageContextProvider _messageContextProvider;
        private readonly ClaimsPrincipal _fallback;

        public RebusPrincipalContextWithFallbackProvider(IMessageContextProvider messageContextProvider)
        {
            _messageContextProvider = messageContextProvider;
            _fallback = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "SYSTEM")
                ], "SYSTEM"));
        }

        public ClaimsPrincipal Current => _messageContextProvider.Current?.IncomingStepContext.Load<ClaimsPrincipal>() ?? _fallback;
    }
}
