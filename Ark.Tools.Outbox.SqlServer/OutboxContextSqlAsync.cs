using Ark.Tools.Sql;

using Dapper;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    internal sealed class OutboxContextSqlAsync<Tag> : IOutboxContextAsync
    {
        private readonly ISqlContextAsync<Tag> _sqlContext;
        private readonly IOutboxContextSqlConfig _config;
        private readonly Statements _statements;

        private static readonly HeaderSerializer _headerSerializer = new HeaderSerializer();

        public DbConnection Connection => _sqlContext.Connection;
        public DbTransaction? Transaction => _sqlContext.Transaction ?? null;

        public OutboxContextSqlAsync(ISqlContextAsync<Tag> sqlContextParent, IOutboxContextSqlConfig config)
        {
            _sqlContext = sqlContextParent;
            _config = config;
            _statements = new Statements(config);
        }

        class Statements
        {
            internal Statements(IOutboxContextSqlConfig config)
            {
                var schema = config.SchemaName ?? "dbo";
                var table = config.TableName;
                var full = $"[{schema}].[{table}]";
                Insert = $@"
                    INSERT INTO {full}
                    (
                          [Headers]
                        , [Body]
                    )
                    VALUES
                    (
                          @pHeaders
                        , @pBody
                    )
                    ";

                PeekLock = (int messageCount) => $@"
                    ;WITH batch AS (
                        SELECT TOP ({messageCount}) *
                        FROM {full}
                        ORDER BY Id DESC)
                    DELETE FROM batch
                    WITH (READPAST, ROWLOCK, READCOMMITTEDLOCK)
                    OUTPUT
                          DELETED.[Headers]
                        , DELETED.[Body]
                    ";

                Count = $@"
                    SELECT COUNT(*) as [Count] 
                    FROM {full}
                    ";

                Clear = $@"
                    DELETE FROM {full}
                    ";

                CreateTable = $@"
                    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schema}')
                    BEGIN
                      EXEC('CREATE SCHEMA {schema}')
                    END

                    IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = '{schema}' AND TABLE_NAME = '{table}')
                        CREATE TABLE {full} (
                            [Id] [bigint] IDENTITY(1,1) NOT NULL,
                          [Headers] [nvarchar](MAX) NOT NULL,
                          [Body] [varbinary](MAX) NOT NULL,
                            CONSTRAINT [PK_{table}] PRIMARY KEY CLUSTERED 
                            (
                              [Id] ASC
                            )
                        )
  
                    ";

            }

            public string Insert { get; }
            public Func<int, string> PeekLock { get; }
            public string Count { get; }
            public string Clear { get; }
            public string CreateTable { get; }
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

                var cmd = new CommandDefinition(_statements.Insert, parameters, transaction: this.Transaction, cancellationToken: ctk);

                _ = await Connection.ExecuteAsync(cmd);
            }
        }

        public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.PeekLock(messageCount), transaction: this.Transaction, cancellationToken: ctk);

            var res = await Connection.QueryAsync<(string Headers, byte[] Body)>(cmd);

            return res.Select(x => new OutboxMessage
            {
                Body = x.Body,
                Headers = _headerSerializer.DeserializeFromString(x.Headers)
            }).ToList();
        }

        public async Task<int> CountAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.Count, transaction: this.Transaction, cancellationToken: ctk);

            return await this.Connection.QuerySingleAsync<int>(cmd);
        }

        public async Task<int> ClearAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.Clear, transaction: this.Transaction, cancellationToken: ctk);

            return await this.Connection.ExecuteAsync(cmd);
        }

        public async ValueTask CommitAysnc(CancellationToken ctk)
        {
            await _sqlContext.CommitAysnc(ctk);
        }

        public async ValueTask DisposeAsync()
        {
            await _sqlContext.DisposeAsync();
        }
    }
}
