
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox
{
    public interface IOutboxContextAsyncFactory
    {
        Task<IOutboxContextAsync> CreateAsync(CancellationToken ctk = default);
    }
}
