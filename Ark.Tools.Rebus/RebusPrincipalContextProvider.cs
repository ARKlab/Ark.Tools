using Ark.Tools.Solid;
using System.Security.Claims;


namespace Ark.Tools.Rebus
{
    public class RebusPrincipalContextProvider : IContextProvider<ClaimsPrincipal?>
    {
        private readonly IMessageContextProvider _messageContextProvider;
        public RebusPrincipalContextProvider(IMessageContextProvider messageContextProvider)
        {
            _messageContextProvider = messageContextProvider;
        }
        public ClaimsPrincipal? Current => _messageContextProvider.Current?.IncomingStepContext?.Load<ClaimsPrincipal>();
    }
}
