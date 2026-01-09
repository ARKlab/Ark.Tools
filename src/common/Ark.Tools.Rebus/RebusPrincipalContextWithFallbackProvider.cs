using Ark.Tools.Solid;

using System.Security.Claims;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
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



=======
namespace Ark.Tools.Rebus;

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
>>>>>>> After
    namespace Ark.Tools.Rebus;

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