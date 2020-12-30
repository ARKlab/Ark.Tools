using Ark.Tools.Sql;

using Dapper;

using MoreLinq;

using NLog;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Outbox.SqlServer
{
    public sealed class OutboxContextSql<Tag> : IOutboxContext
    {
        private readonly ISqlContext<Tag> _sqlContext;
        private readonly IOutboxContextSqlConfig _config;
        private readonly Statements _statements;

        public IDbConnection Connection => _sqlContext.Connection;
        public IDbTransaction Transaction => _sqlContext.Transaction;

        public OutboxContextSql(ISqlContext<Tag> sqlContextParent, IOutboxContextSqlConfig config)
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
                    pHeaders = message.Headers,
                    pBody = message.Body
                });

                var cmd = new CommandDefinition(_statements.Insert, parameters, transaction: this.Transaction, cancellationToken: ctk);

                _ = await this.Connection.ExecuteAsync(cmd).ConfigureAwait(false);
            }
        }

        public Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.PeekLock(messageCount), transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.QueryAsync<OutboxMessage>(cmd);
        }

        public Task<int> CountAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.Count, transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.QuerySingleAsync<int>(cmd);
        }

        public Task ClearAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.Clear, transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.ExecuteAsync(cmd);
        }

        public Task EnsureTableAreCreated(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.CreateTable, transaction: this.Transaction, cancellationToken: ctk);

            return this.Connection.ExecuteAsync(cmd);
        }

        public void Commit()
        {
            _sqlContext.Commit();
        }

        public void Dispose()
        {
            _sqlContext.Dispose();
        }
    }
}
