using Dapper;

using MoreLinq;

using System.Data;

namespace Ark.Tools.Outbox.SqlServer;


internal abstract class OutboxContextSqlCore
{
    private readonly IOutboxContextSqlConfig _config;
    private readonly Statements _statements;

    private static readonly HeaderSerializer _headerSerializer = new();

    private protected OutboxContextSqlCore(IOutboxContextSqlConfig config)
    {
        _config = config;
        _statements = new Statements(config);
    }

    private protected abstract IDbTransaction _transaction { get; }
    private protected abstract IDbConnection _connection { get; }

    sealed class Statements
    {
        internal Statements(IOutboxContextSqlConfig config)
        {
            var schema = config.SchemaName ?? "dbo";
            var table = config.TableName;
            var full = $"[{schema}].[{table}]";
            // A multi-row VALUES alone does not fix IDENTITY order; INSERT ... SELECT ... ORDER BY does.
            Insert = (int messageCount) => $@"
                INSERT INTO {full}
                (
                      [Headers]
                    , [Body]
                )
                SELECT [Headers], [Body]
                FROM (VALUES {string.Join(", ", Enumerable.Range(0, messageCount).Select(static i => string.Create(CultureInfo.InvariantCulture, $"(@h{i}, @b{i}, {i})")))})
                    AS v([Headers], [Body], [Ord])
                ORDER BY [Ord]
                ";

            PeekLock = (int messageCount) => $@"
                -- otel-query-label: outbox.peek-lock
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

        public Func<int, string> Insert { get; }
        public Func<int, string> PeekLock { get; }
        public string Count { get; }
        public string Clear { get; }
        public string CreateTable { get; }
    }

    public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
    {
        // One statement per chunk; 2 parameters per row keeps 1000 rows under the SQL Server 2100-parameter limit.
        foreach (var b in messages.Batch(1000))
        {
            var parameters = new DynamicParameters();
            for (var i = 0; i < b.Length; i++)
            {
                parameters.Add(string.Create(CultureInfo.InvariantCulture, $"h{i}"), _headerSerializer.SerializeToString(b[i].Headers), DbType.String, size: -1);
                parameters.Add(string.Create(CultureInfo.InvariantCulture, $"b{i}"), b[i].Body, DbType.Binary, size: -1);
            }

            var cmd = new CommandDefinition(_statements.Insert(b.Length), parameters, transaction: _transaction, cancellationToken: ctk);

            _ = await _connection.ExecuteAsync(cmd).ConfigureAwait(false);
        }
    }

    public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
    {
        var cmd = new CommandDefinition(_statements.PeekLock(messageCount), transaction: _transaction, cancellationToken: ctk);

        var res = await _connection.QueryAsync<(string Headers, byte[] Body)>(cmd).ConfigureAwait(false);

        return res.Select(static x => new OutboxMessage
        {
            Body = x.Body,
            Headers = _headerSerializer.DeserializeFromString(x.Headers)
        }).ToList();
    }

    public async Task<int> CountAsync(CancellationToken ctk = default)
    {
        var cmd = new CommandDefinition(_statements.Count, transaction: _transaction, cancellationToken: ctk);

        return await _connection.QuerySingleAsync<int>(cmd).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken ctk = default)
    {
        var cmd = new CommandDefinition(_statements.Clear, transaction: _transaction, cancellationToken: ctk);

        await _connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

    public async Task EnsureTableAreCreated(CancellationToken ctk = default)
    {
        var cmd = new CommandDefinition(_statements.CreateTable, transaction: _transaction, cancellationToken: ctk);

        await _connection.ExecuteAsync(cmd).ConfigureAwait(false);
    }

}