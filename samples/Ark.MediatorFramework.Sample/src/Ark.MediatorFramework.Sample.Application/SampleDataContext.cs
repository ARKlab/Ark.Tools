// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Outbox.Rebus;
using Ark.Tools.Core;

using Dapper;

using NodaTime.Text;
using NodaTime;

using Rebus.Bus;

using System.Data.Common;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>SQL configuration used by the mediator sample.</summary>
public sealed class SampleDataContextConfig : IOutboxContextSqlConfig, Ark.Tools.Sql.ISqlContextConfig
{
    /// <summary>Initializes a new instance of the <see cref="SampleDataContextConfig"/> class.</summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    public SampleDataContextConfig(string connectionString)
    {
        ConnectionString = connectionString;
    }

    /// <inheritdoc />
    public string ConnectionString { get; }

    /// <inheritdoc />
    public string TableName => "Outbox";

    /// <inheritdoc />
    public string SchemaName => "dbo";

    /// <inheritdoc />
    public System.Data.IsolationLevel? IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
}

/// <summary>Transactional SQL context for greetings and Rebus outbox messages.</summary>
public sealed class SampleDataContext : AbstractSqlAsyncContextWithOutbox<SampleDataContext>
{
    /// <summary>Initializes a new instance of the <see cref="SampleDataContext"/> class.</summary>
    /// <param name="transaction">The transaction to use.</param>
    /// <param name="config">The SQL and outbox configuration.</param>
    public SampleDataContext(DbTransaction transaction, IOutboxContextSqlConfig config)
        : base(transaction, config)
    {
    }

    /// <summary>Saves a greeting in the current transaction.</summary>
    public async Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default)
    {
        const string sql = """
            MERGE [dbo].[Greeting] AS target
            USING (SELECT @Id AS [Id]) AS source ON target.[Id] = source.[Id]
            WHEN MATCHED THEN UPDATE SET [Message] = @Message, [Date] = @Date,
                [DateTime] = @DateTime, [OffsetDateTime] = @OffsetDateTime, [Period] = @Period,
                [AuditId] = @AuditId
            WHEN NOT MATCHED THEN INSERT ([Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period], [AuditId])
                VALUES (@Id, @Message, @Date, @DateTime, @OffsetDateTime, @Period, @AuditId);
            """;
        var command = new CommandDefinition(sql, new
        {
            greeting.Id,
            greeting.Message,
            greeting.Date,
            greeting.DateTime,
            greeting.OffsetDateTime,
            Period = PeriodPattern.NormalizingIso.Format(greeting.Period),
            greeting.AuditId,
        }, Transaction, cancellationToken: ctk);
        await Connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <summary>Saves an audit record in the current transaction.</summary>
    public async Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Audit] ([Id], [UserId], [EntityType], [Identifier], [Operation], [Timestamp])
            VALUES (@Id, @UserId, @EntityType, @Identifier, @Operation, @Timestamp);
            """;
        var command = new CommandDefinition(sql, new
        {
            audit.Id,
            audit.UserId,
            audit.EntityType,
            audit.Identifier,
            audit.Operation,
            audit.Timestamp,
        }, Transaction, cancellationToken: ctk);
        await Connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <summary>Reads a greeting in the current transaction.</summary>
    public async Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default)
    {
        const string sql = "SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period], [AuditId] FROM [dbo].[Greeting] WHERE [Id] = @Id";
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ctk);
        var row = await Connection.QuerySingleOrDefaultAsync<GreetingRow>(command).ConfigureAwait(false);
        return row?.ToResponse();
    }

    /// <summary>Reads all greetings in the current transaction.</summary>
    public async Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default)
    {
        const string sql = "SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period], [AuditId] FROM [dbo].[Greeting]";
        var command = new CommandDefinition(sql, transaction: Transaction, cancellationToken: ctk);
        var rows = await Connection.QueryAsync<GreetingRow>(command).ConfigureAwait(false);
        return rows.Select(row => row.ToResponse()).ToArray();
    }

    /// <summary>Reads a page of audit records in the current transaction.</summary>
    public async Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default)
    {
        var where = """
            WHERE (@UserId IS NULL OR [UserId] = @UserId)
              AND (@EntityType IS NULL OR [EntityType] = @EntityType)
              AND (@Identifier IS NULL OR [Identifier] = @Identifier)
              AND (@FromTimestamp IS NULL OR [Timestamp] >= @FromTimestamp)
              AND (@ToTimestamp IS NULL OR [Timestamp] <= @ToTimestamp)
            """;
        var orderBy = BuildAuditOrderBy(query.Sort ?? []);
        var sql = $"""
            SELECT [Id], [UserId], [EntityType], [Identifier], [Operation], [Timestamp]
            FROM [dbo].[Audit]
            {where}
            ORDER BY {orderBy}
            OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY;
            SELECT COUNT_BIG(*) FROM [dbo].[Audit]
            {where};
            """;
        var parameters = new
        {
            query.UserId,
            query.EntityType,
            query.Identifier,
            FromTimestamp = ParseTimestamp(query.FromTimestamp),
            ToTimestamp = ParseTimestamp(query.ToTimestamp),
            query.Skip,
            query.Limit,
        };
        var command = new CommandDefinition(sql, parameters, Transaction, cancellationToken: ctk);
        await using var results = await Connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var records = await results.ReadAsync<AuditRecord>().ConfigureAwait(false);
        var count = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        return new PagedResult<AuditRecord>
        {
            Count = count,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = records.ToArray(),
        };
    }

    private static string BuildAuditOrderBy(IEnumerable<string> sorts)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(AuditRecord.Id)] = "[Id]",
            [nameof(AuditRecord.UserId)] = "[UserId]",
            [nameof(AuditRecord.EntityType)] = "[EntityType]",
            [nameof(AuditRecord.Identifier)] = "[Identifier]",
            [nameof(AuditRecord.Operation)] = "[Operation]",
            [nameof(AuditRecord.Timestamp)] = "[Timestamp]",
        };
        var orderBy = sorts
            .Where(sort => !string.IsNullOrWhiteSpace(sort))
            .Select(sort =>
            {
                var parts = sort.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2 || !columns.TryGetValue(parts[0], out var column))
                    throw new ArgumentException($"Invalid audit sort '{sort}'.", nameof(sorts));
                var direction = parts.Length == 2
                    ? parts[1].ToUpperInvariant() switch
                    {
                        "ASC" => " ASC",
                        "DESC" => " DESC",
                        _ => throw new ArgumentException($"Invalid audit sort direction '{parts[1]}'.", nameof(sorts)),
                    }
                    : string.Empty;
                return column + direction;
            })
            .ToArray();
        return orderBy.Length == 0 ? "[Timestamp] DESC" : string.Join(", ", orderBy);
    }

    private static Instant? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            return InstantPattern.ExtendedIso.Parse(value).Value;
        }
        catch (UnparsableValueException exception)
        {
            throw new ArgumentException($"Invalid audit timestamp '{value}'.", nameof(value), exception);
        }
    }

    private sealed class GreetingRow
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public NodaTime.LocalDate Date { get; set; }
        public NodaTime.LocalDateTime DateTime { get; set; }
        public NodaTime.OffsetDateTime OffsetDateTime { get; set; }
        public string Period { get; set; } = string.Empty;
        public Guid AuditId { get; set; }

        public GreetingResponse ToResponse()
        {
            return new GreetingResponse
            {
                Id = Id,
                Message = Message,
                Date = Date,
                DateTime = DateTime,
                OffsetDateTime = OffsetDateTime,
                Period = PeriodPattern.NormalizingIso.Parse(Period).Value,
                AuditId = AuditId,
            };
        }
    }
}

/// <summary>Creates transactional sample SQL contexts.</summary>
public sealed class SampleDataContextFactory : Ark.Tools.Sql.AbstractSqlAsyncContextFactory<SampleDataContext, SampleDataContext>, Ark.Tools.Outbox.IOutboxAsyncContextFactory
{
    private readonly SampleDataContextConfig _config;

    /// <summary>Initializes a new instance of the <see cref="SampleDataContextFactory"/> class.</summary>
    /// <param name="connectionManager">The SQL connection manager.</param>
    /// <param name="config">The sample database configuration.</param>
    public SampleDataContextFactory(Ark.Tools.Sql.IDbConnectionManager connectionManager, SampleDataContextConfig config)
        : base(connectionManager, config)
    {
        _config = config;
    }

    /// <inheritdoc />
    protected override SampleDataContext CreateContext(DbTransaction transaction)
    {
        return new SampleDataContext(transaction, _config);
    }

    async Task<Ark.Tools.Outbox.IOutboxAsyncContext> Ark.Tools.Outbox.IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
    {
        return await CreateAsync(ctk).ConfigureAwait(false);
    }
}

/// <summary>SQL-backed greeting store with one transaction per operation.</summary>
public sealed class SqlGreetingStore : IGreetingStore
{
    private readonly SampleDataContextFactory _factory;
    private readonly IBus _bus;

    /// <summary>Initializes a new instance of the <see cref="SqlGreetingStore"/> class.</summary>
    /// <param name="factory">The sample context factory.</param>
    /// <param name="bus">The Rebus bus used by the transactional outbox.</param>
    public SqlGreetingStore(SampleDataContextFactory factory, IBus bus)
    {
        _factory = factory;
        _bus = bus;
    }

    /// <inheritdoc />
    /// <param name="greeting">The greeting to persist.</param>
    /// <param name="audit">The optional audit entry to persist in the transaction.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        await context.SaveAsync(greeting, ctk).ConfigureAwait(false);
        using var scope = _bus.Enlist(context);
        await _bus.SendLocal(new GreetingCreatedNotification { Greeting = greeting }).ConfigureAwait(false);
        await scope.CompleteAsync().ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <param name="greeting">The greeting to persist.</param>
    /// <param name="audit">The optional audit entry to persist in the transaction.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task SaveAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        await context.SaveAsync(greeting, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadAuditsAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }

    /// <inheritdoc />
    public async Task<GreetingResponse> GetAsync(Guid id, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var greeting = await context.ReadAsync(id, ctk).ConfigureAwait(false);
        if (greeting is null)
            throw new Ark.Tools.Core.EntityNotFoundException($"Greeting '{id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return greeting;
    }

    /// <inheritdoc />
    public async Task<GreetingResponse?> TryGetAsync(Guid id, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var greeting = await context.ReadAsync(id, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return greeting;
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var greetings = await context.ReadAllAsync(ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return greetings.Count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<GreetingResponse>> AllAsync(CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var greetings = await context.ReadAllAsync(ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return greetings;
    }
}
