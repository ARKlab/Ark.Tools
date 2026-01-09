using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Outbox(net10.0)', Before:
namespace Ark.Tools.Outbox
{
    public interface IOutboxAsyncContextFactory
    {
        Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default);
    }
=======
namespace Ark.Tools.Outbox;

public interface IOutboxAsyncContextFactory
{
    Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default);
>>>>>>> After


namespace Ark.Tools.Outbox;

public interface IOutboxAsyncContextFactory
{
    Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default);
}