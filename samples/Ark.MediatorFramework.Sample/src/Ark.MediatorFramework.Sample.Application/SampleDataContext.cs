// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Outbox.Rebus;

using Dapper;

using NodaTime.Text;

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
                [DateTime] = @DateTime, [OffsetDateTime] = @OffsetDateTime, [Period] = @Period
            WHEN NOT MATCHED THEN INSERT ([Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period])
                VALUES (@Id, @Message, @Date, @DateTime, @OffsetDateTime, @Period);
            """;
        var command = new CommandDefinition(sql, new
        {
            greeting.Id,
            greeting.Message,
            Date = LocalDatePattern.Iso.Format(greeting.Date),
            DateTime = LocalDateTimePattern.ExtendedIso.Format(greeting.DateTime),
            OffsetDateTime = OffsetDateTimePattern.ExtendedIso.Format(greeting.OffsetDateTime),
            Period = PeriodPattern.NormalizingIso.Format(greeting.Period),
        }, Transaction, cancellationToken: ctk);
        await Connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <summary>Reads a greeting in the current transaction.</summary>
    public async Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default)
    {
        const string sql = "SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period] FROM [dbo].[Greeting] WHERE [Id] = @Id";
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ctk);
        var row = await Connection.QuerySingleOrDefaultAsync<GreetingRow>(command).ConfigureAwait(false);
        return row?.ToResponse();
    }

    /// <summary>Reads all greetings in the current transaction.</summary>
    public async Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default)
    {
        const string sql = "SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period] FROM [dbo].[Greeting]";
        var command = new CommandDefinition(sql, transaction: Transaction, cancellationToken: ctk);
        var rows = await Connection.QueryAsync<GreetingRow>(command).ConfigureAwait(false);
        return rows.Select(row => row.ToResponse()).ToArray();
    }

    private sealed class GreetingRow
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;
        public string OffsetDateTime { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;

        public GreetingResponse ToResponse()
        {
            return new GreetingResponse
            {
                Id = Id,
                Message = Message,
                Date = LocalDatePattern.Iso.Parse(Date).Value,
                DateTime = LocalDateTimePattern.ExtendedIso.Parse(DateTime).Value,
                OffsetDateTime = OffsetDateTimePattern.ExtendedIso.Parse(OffsetDateTime).Value,
                Period = PeriodPattern.NormalizingIso.Parse(Period).Value,
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
    public async Task SaveAndPublishAsync(GreetingResponse greeting, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.SaveAsync(greeting, ctk).ConfigureAwait(false);
        using var scope = _bus.Enlist(context);
        await _bus.SendLocal(new GreetingCreatedNotification { Greeting = greeting }).ConfigureAwait(false);
        await scope.CompleteAsync().ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.SaveAsync(greeting, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
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
