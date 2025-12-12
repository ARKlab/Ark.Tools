using Ark.Tools.Sql;

using System.Data;

namespace Ark.Tools.Outbox.SqlServer
{

    internal sealed class OutboxContextSql<Tag> : OutboxContextSqlCore
    {
        private readonly ISqlContext<Tag> _context;

        public OutboxContextSql(ISqlContext<Tag> context, IOutboxContextSqlConfig config) : base(config)
        {
            _context = context;
        }

        protected override IDbTransaction _transaction => _context.Transaction;

        protected override IDbConnection _connection => _context.Connection;
    }
}
