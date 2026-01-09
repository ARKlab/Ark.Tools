
namespace Ark.Tools.Outbox;

public interface IOutboxAsyncContextFactory
{
    Task<IOutboxAsyncContext> CreateAsync(CancellationToken ctk = default);
}