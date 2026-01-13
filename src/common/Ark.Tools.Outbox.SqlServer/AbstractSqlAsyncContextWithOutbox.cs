using Ark.Tools.Sql;

using System.Data.Common;

namespace Ark.Tools.Outbox.SqlServer;

public abstract class AbstractSqlAsyncContextWithOutbox<TTag> : AbstractSqlAsyncContext<TTag>, IOutboxAsyncContext
{
    private readonly OutboxAsyncContextSql<TTag> _outbox;

    protected AbstractSqlAsyncContextWithOutbox(DbTransaction transaction, IOutboxContextSqlConfig config)
        : base(transaction)
    {
        _outbox = new OutboxAsyncContextSql<TTag>(this, config);
    }

    public async Task ClearAsync(CancellationToken ctk = default)
    {
        await _outbox.ClearAsync(ctk).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken ctk = default)
    {
        return await _outbox.CountAsync(ctk).ConfigureAwait(false);
    }

    public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
    {
        return await _outbox.PeekLockMessagesAsync(messageCount, ctk).ConfigureAwait(false);
    }

    public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
    {
        await _outbox.SendAsync(messages, ctk).ConfigureAwait(false);
    }

}