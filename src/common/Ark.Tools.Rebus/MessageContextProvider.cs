
namespace Ark.Tools.Rebus;

public sealed class MessageContextProvider : IMessageContextProvider
{
    public IMessageContext Current => MessageContext.Current;
}