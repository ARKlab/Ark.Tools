using Rebus.Pipeline;

namespace Ark.Tools.Rebus
{
	public class MessageContextProvider : IMessageContextProvider
    {
        public IMessageContext Current => MessageContext.Current;
    }
}
