using Ark.Tools.Sql;

using System.Data;

namespace Ark.Tools.Outbox.SqlServer;

internal sealed class OutboxAsyncContextSql<Tag> : OutboxContextSqlCore
{
    private readonly ISqlAsyncContext<Tag> _context;

    public OutboxAsyncContextSql(ISqlAsyncContext<Tag> context, IOutboxContextSqlConfig config) : base(config)
    {
        _context = context;
    }

    protected override IDbTransaction _transaction => _context.Transaction;

    protected override IDbConnection _connection => _context.Connection;
}
