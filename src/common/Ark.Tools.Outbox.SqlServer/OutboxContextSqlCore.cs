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
    internal abstract class OutboxContextSqlCore
    {
        private readonly IOutboxContextSqlConfig _config;
        private readonly Statements _statements;

        private static readonly HeaderSerializer _headerSerializer = new();

        protected OutboxContextSqlCore(IOutboxContextSqlConfig config)
        {
            _config = config;
            _statements = new Statements(config);
        }

        protected abstract IDbTransaction _transaction { get; }
        protected abstract IDbConnection _connection { get; }

        sealed class Statements
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

                var cmd = new CommandDefinition(_statements.Insert, parameters, transaction: _transaction, cancellationToken: ctk);

                _ = await _connection.ExecuteAsync(cmd).ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.PeekLock(messageCount), transaction: _transaction, cancellationToken: ctk);

            var res = await _connection.QueryAsync<(string Headers, byte[] Body)>(cmd).ConfigureAwait(false);

            return res.Select(x => new OutboxMessage
            {
                Body = x.Body,
                Headers = _headerSerializer.DeserializeFromString(x.Headers)
            }).ToList();
        }

        public Task<int> CountAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.Count, transaction: _transaction, cancellationToken: ctk);

            return _connection.QuerySingleAsync<int>(cmd);
        }

        public Task ClearAsync(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.Clear, transaction: _transaction, cancellationToken: ctk);

            return _connection.ExecuteAsync(cmd);
        }

        public Task EnsureTableAreCreated(CancellationToken ctk = default)
        {
            var cmd = new CommandDefinition(_statements.CreateTable, transaction: _transaction, cancellationToken: ctk);

            return _connection.ExecuteAsync(cmd);
        }

    }
}