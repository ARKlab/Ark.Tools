using Ark.Tools.Sql;

using Dapper;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    internal sealed class OutboxContextSql<Tag> : BaseOutboxContextStatements, IOutboxContext
    {
        private readonly ISqlContext<Tag> _sqlContext;
        private readonly IOutboxContextSqlConfig _config;

        private static readonly HeaderSerializer _headerSerializer = new HeaderSerializer();

        public IDbConnection Connection => _sqlContext.Connection;
        public IDbTransaction Transaction => _sqlContext.Transaction;

        public OutboxContextSql(ISqlContext<Tag> sqlContextParent, IOutboxContextSqlConfig config): base(config)
        {
            _sqlContext = sqlContextParent;
            _config = config;
        }

        public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
        {
            foreach (var b in messages.Batch(1000))
            {
                var parameters = b.Select(message => new
                {
                    pHeaders = _headerSerializer.SerializeToString(message.Headers),
                    pBody = message.Body
                });

                var cmd = new CommandDefinition(base.Insert, parameters, transaction: this.Transaction, cancellationToken: ctk);

                _ = await this.Connection.ExecuteAsync(cmd);
            }
        }

        public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(base.PeekLock(messageCount), transaction: this.Transaction, cancellationToken: ctk);

            var res = await this.Connection.QueryAsync<(string Headers, byte[] Body)>(cmd);

            return res.Select(x => new OutboxMessage
            {
                Body = x.Body,
                Headers = _headerSerializer.DeserializeFromString(x.Headers)
            }).ToList();
        }

        public Task<int> CountAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(base.Count, transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.QuerySingleAsync<int>(cmd);
        }

        public Task ClearAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(base.Clear, transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.ExecuteAsync(cmd);
        }

        public Task EnsureTableAreCreated(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(base.CreateTable, transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.ExecuteAsync(cmd);
        }

        public void Commit()
        {
            _sqlContext.Commit();
        }

        public void Dispose()
        {
        }
    }
}
