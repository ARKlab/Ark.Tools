
namespace Ark.Tools.Outbox
{
    public interface IOutboxContextAsyncFactory
    {
        IOutboxContextAsync Create();
    }
}
