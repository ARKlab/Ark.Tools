using Rebus.Pipeline;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Rebus(net10.0)', Before:
namespace Ark.Tools.Rebus
{
    public class MessageContextProvider : IMessageContextProvider
    {
        public IMessageContext Current => MessageContext.Current;
    }
=======
namespace Ark.Tools.Rebus;

public class MessageContextProvider : IMessageContextProvider
{
    public IMessageContext Current => MessageContext.Current;
>>>>>>> After


namespace Ark.Tools.Rebus;

public class MessageContextProvider : IMessageContextProvider
{
    public IMessageContext Current => MessageContext.Current;
}