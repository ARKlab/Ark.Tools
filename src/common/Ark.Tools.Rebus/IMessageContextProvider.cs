
namespace Ark.Tools.Rebus;

public interface IMessageContextProvider
{
    IMessageContext Current { get; }
}