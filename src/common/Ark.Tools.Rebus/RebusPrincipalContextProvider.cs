using Ark.Tools.Solid;

using System;
using System.Security.Claims;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
namespace Ark.Tools.Rebus
{
    public class RebusPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
    {
        private readonly IMessageContextProvider _messageContextProvider;
        public RebusPrincipalContextProvider(IMessageContextProvider messageContextProvider)
        {
            _messageContextProvider = messageContextProvider;
        }
        public ClaimsPrincipal Current
        {
            get
            {
                var ctx = _messageContextProvider.Current;
                if (ctx == null)
                    throw new InvalidOperationException("MessageContext is null. This happens when trying to execute code outside of a message handler.");
                var incoming = ctx.IncomingStepContext;
                if (incoming == null)
                    throw new InvalidOperationException("IncomingStepContext is null. This happens when trying to execute code outside of a message handler.");

                return incoming.Load<ClaimsPrincipal>();
            }
=======
namespace Ark.Tools.Rebus;

public class RebusPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IMessageContextProvider _messageContextProvider;
    public RebusPrincipalContextProvider(IMessageContextProvider messageContextProvider)
    {
        _messageContextProvider = messageContextProvider;
    }
    public ClaimsPrincipal Current
    {
        get
        {
            var ctx = _messageContextProvider.Current;
            if (ctx == null)
                throw new InvalidOperationException("MessageContext is null. This happens when trying to execute code outside of a message handler.");
            var incoming = ctx.IncomingStepContext;
            if (incoming == null)
                throw new InvalidOperationException("IncomingStepContext is null. This happens when trying to execute code outside of a message handler.");

            return incoming.Load<ClaimsPrincipal>();
>>>>>>> After



namespace Ark.Tools.Rebus;

public class RebusPrincipalContextProvider : IContextProvider<ClaimsPrincipal>
{
    private readonly IMessageContextProvider _messageContextProvider;
    public RebusPrincipalContextProvider(IMessageContextProvider messageContextProvider)
    {
        _messageContextProvider = messageContextProvider;
    }
    public ClaimsPrincipal Current
    {
        get
        {
            var ctx = _messageContextProvider.Current;
            if (ctx == null)
                throw new InvalidOperationException("MessageContext is null. This happens when trying to execute code outside of a message handler.");
            var incoming = ctx.IncomingStepContext;
            if (incoming == null)
                throw new InvalidOperationException("IncomingStepContext is null. This happens when trying to execute code outside of a message handler.");

            return incoming.Load<ClaimsPrincipal>();
        }
    }
}