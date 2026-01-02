using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox
{
    public interface IOutboxAsyncContextFactory
    {
        Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default);
    }
}