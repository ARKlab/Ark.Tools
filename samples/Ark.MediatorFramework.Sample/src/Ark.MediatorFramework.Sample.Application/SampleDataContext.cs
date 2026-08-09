// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Outbox.Rebus;
using Ark.Tools.Outbox;
using Ark.Tools.Core;

using Dapper;

using NodaTime;
using NodaTime.Text;

using Rebus.Bus;

using System.Data.Common;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Composes fine-grained greeting and audit operations in one application transaction.</summary>
public interface ISampleDataContext : IAsyncDisposable
{
    /// <summary>Gets the transactional outbox context for the current data transaction.</summary>
    IOutboxContextCore OutboxContext { get; }

    /// <summary>Saves a greeting.</summary>
    Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default);

    /// <summary>Writes an audit entry.</summary>
    Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default);

    /// <summary>Reads a greeting.</summary>
    Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Reads all greetings.</summary>
    Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default);

    /// <summary>Updates a greeting using its expected ETag.</summary>
    Task<GreetingResponse?> UpdateAsync(Guid id, string message, string eTag, Guid auditId, CancellationToken ctk = default);

    /// <summary>Reads audit records.</summary>
    Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default);

    /// <summary>Reads greetings.</summary>
    Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default);

    /// <summary>Commits the transaction.</summary>
    Task CommitAsync(CancellationToken ctk = default);

    /// <summary>Saves a book.</summary>
    Task SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default);

    /// <summary>Reads a book.</summary>
    Task<Book.V1.Output?> ReadBookAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book.</summary>
    Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default);

    /// <summary>Deletes a book.</summary>
    Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Reads a page of books.</summary>
    Task<Book.V1.Page> ReadBooksAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default);

    /// <summary>Saves a book print process when no active process exists for the book.</summary>
    Task<bool> TrySaveBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default);

    /// <summary>Reads a book print process.</summary>
    Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book print process.</summary>
    Task<bool> UpdateBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default);
}

/// <summary>Creates application contexts for handler-owned transactions.</summary>
public interface ISampleDataContextFactory
{
    /// <summary>Creates a context.</summary>
    Task<ISampleDataContext> CreateAsync(CancellationToken ctk = default);
}

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
public sealed class SampleDataContext : AbstractSqlAsyncContextWithOutbox<SampleDataContext>, ISampleDataContext
{
    /// <inheritdoc />
    public Ark.Tools.Outbox.IOutboxContextCore OutboxContext => this;

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
            Period = PeriodPattern.NormalizingIso.Format(greeting.Period ?? Period.Zero),
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
        const string sql = """
            SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period], [AuditId],
                   [RowVersion] AS [ETag]
            FROM [dbo].[Greeting]
            WHERE [Id] = @Id
            """;
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ctk);
        var row = await Connection.QuerySingleOrDefaultAsync<GreetingRow>(command).ConfigureAwait(false);
        return row?.ToResponse();
    }

    /// <summary>Reads all greetings in the current transaction.</summary>
    public async Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period], [AuditId],
                   [RowVersion] AS [ETag]
            FROM [dbo].[Greeting]
            """;
        var command = new CommandDefinition(sql, transaction: Transaction, cancellationToken: ctk);
        var rows = await Connection.QueryAsync<GreetingRow>(command).ConfigureAwait(false);
        return rows.Select(row => row.ToResponse()).ToArray();
    }

    /// <summary>Updates a greeting conditionally and returns its new opaque ETag.</summary>
    public async Task<GreetingResponse?> UpdateAsync(
        Guid id,
        string message,
        string eTag,
        Guid auditId,
        CancellationToken ctk = default)
    {
        const string sql = """
            MERGE [dbo].[Greeting] AS target
            USING (SELECT @Id AS [Id]) AS source ON target.[Id] = source.[Id]
            WHEN MATCHED AND target.[RowVersion] = TRY_CONVERT(VARBINARY(8), @ETag, 1) THEN
                UPDATE SET [Message] = @Message, [AuditId] = @AuditId
            OUTPUT inserted.[Id], inserted.[Message], inserted.[Date], inserted.[DateTime],
                   inserted.[OffsetDateTime], inserted.[Period], inserted.[AuditId],
                   inserted.[RowVersion] AS [ETag];
            """;
        var command = new CommandDefinition(sql, new { Id = id, Message = message, AuditId = auditId, ETag = eTag }, Transaction, cancellationToken: ctk);
        var row = await Connection.QuerySingleOrDefaultAsync<GreetingRow>(command).ConfigureAwait(false);
        return row?.ToResponse();
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
            query.FromTimestamp,
            query.ToTimestamp,
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

    /// <summary>Reads a page of greetings in the current transaction.</summary>
    public async Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [Message], [Date], [DateTime], [OffsetDateTime], [Period], [AuditId],
                   [RowVersion] AS [ETag]
            FROM [dbo].[Greeting]
            WHERE (@MessageContains IS NULL OR [Message] LIKE '%' + @MessageContains + '%' ESCAPE '\')
            ORDER BY [Id]
            OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY;
            SELECT COUNT_BIG(*)
            FROM [dbo].[Greeting]
            WHERE (@MessageContains IS NULL OR [Message] LIKE '%' + @MessageContains + '%' ESCAPE '\');
            """;
        var command = new CommandDefinition(sql, new
        {
            MessageContains = query.MessageContains is null ? null : EscapeLikePattern(query.MessageContains),
            query.Skip,
            query.Limit,
        }, Transaction, cancellationToken: ctk);
        await using var results = await Connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var rows = await results.ReadAsync<GreetingRow>().ConfigureAwait(false);
        var count = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        return new GreetingPage
        {
            Count = count,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = rows.Select(row => row.ToResponse()).ToArray(),
        };
    }

    /// <summary>Saves a book in the current transaction.</summary>
    public async Task SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Book] ([Id], [Title], [Author], [Genre], [ISBN], [Description])
            VALUES (@Id, @Title, @Author, @Genre, @ISBN, @Description);
            """;
        var command = new CommandDefinition(sql, new
        {
            book.Id,
            book.Title,
            book.Author,
            book.Genre,
            book.ISBN,
            book.Description,
        }, Transaction, cancellationToken: ctk);
        await Connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <summary>Reads a book by identifier in the current transaction.</summary>
    public async Task<Book.V1.Output?> ReadBookAsync(Guid id, CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [Title], [Author], [Genre], [ISBN], [Description]
            FROM [dbo].[Book]
            WHERE [Id] = @Id;
            """;
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ctk);
        var row = await Connection.QuerySingleOrDefaultAsync<BookRow>(command).ConfigureAwait(false);
        return row?.ToResponse();
    }

    /// <summary>Updates a book in the current transaction.</summary>
    public async Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default)
    {
        const string sql = """
            UPDATE [dbo].[Book]
            SET [Title] = @Title,
                [Author] = @Author,
                [Genre] = @Genre,
                [ISBN] = @ISBN,
                [Description] = @Description
            WHERE [Id] = @Id;
            """;
        var command = new CommandDefinition(sql, new
        {
            book.Id,
            book.Title,
            book.Author,
            book.Genre,
            book.ISBN,
            book.Description,
        }, Transaction, cancellationToken: ctk);
        return await Connection.ExecuteAsync(command).ConfigureAwait(false) == 1;
    }

    /// <summary>Deletes a book in the current transaction.</summary>
    public async Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default)
    {
        const string sql = """
            DELETE FROM [dbo].[Book]
            WHERE [Id] = @Id;
            """;
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ctk);
        return await Connection.ExecuteAsync(command).ConfigureAwait(false) == 1;
    }

    /// <summary>Reads a page of books in the current transaction.</summary>
    public async Task<Book.V1.Page> ReadBooksAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [Title], [Author], [Genre], [ISBN], [Description]
            FROM [dbo].[Book]
            WHERE (@Title IS NULL OR [Title] = @Title)
              AND (@Author IS NULL OR [Author] = @Author)
              AND (@Genre IS NULL OR [Genre] = @Genre)
            ORDER BY [Id]
            OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY;
            SELECT COUNT_BIG(*)
            FROM [dbo].[Book]
            WHERE (@Title IS NULL OR [Title] = @Title)
              AND (@Author IS NULL OR [Author] = @Author)
              AND (@Genre IS NULL OR [Genre] = @Genre);
            """;
        var command = new CommandDefinition(sql, new
        {
            query.Title,
            query.Author,
            query.Genre,
            query.Skip,
            query.Limit,
        }, Transaction, cancellationToken: ctk);
        await using var results = await Connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var rows = await results.ReadAsync<BookRow>().ConfigureAwait(false);
        var count = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        return new Book.V1.Page
        {
            Count = count,
            Skip = query.Skip,
            Limit = query.Limit,
            Data = rows.Select(row => row.ToResponse()).ToArray(),
        };
    }

    /// <summary>Saves a book print process when no pending or running process exists for the book.</summary>
    public async Task<bool> TrySaveBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
    {
        const string sql = """
            INSERT INTO [dbo].[BookPrintProcess] ([Id], [BookId], [Progress], [Status], [IsActive], [ErrorMessage], [ShouldFail])
            SELECT @Id, @BookId, @Progress, @Status, 1, @ErrorMessage, @ShouldFail
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [dbo].[BookPrintProcess] WITH (UPDLOCK, HOLDLOCK)
                WHERE [BookId] = @BookId
                  AND [Status] IN (@Pending, @Running)
            );
            """;
        var command = new CommandDefinition(sql, new
        {
            process.Id,
            process.BookId,
            process.Progress,
            process.Status,
            process.ErrorMessage,
            process.ShouldFail,
            Pending = (EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Pending,
            Running = (EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Running,
        }, Transaction, cancellationToken: ctk);
        return await Connection.ExecuteAsync(command).ConfigureAwait(false) == 1;
    }

    /// <summary>Reads a book print process in the current transaction.</summary>
    public async Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(Guid id, CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [BookId], [Progress], [Status], [ErrorMessage], [ShouldFail]
            FROM [dbo].[BookPrintProcess]
            WHERE [Id] = @Id;
            """;
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ctk);
        return await Connection.QuerySingleOrDefaultAsync<BookPrintProcessResponse>(command).ConfigureAwait(false);
    }

    /// <summary>Updates a book print process in the current transaction.</summary>
    public async Task<bool> UpdateBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
    {
        const string sql = """
            UPDATE [dbo].[BookPrintProcess]
            SET [Progress] = @Progress,
                [Status] = @Status,
                [IsActive] = CASE WHEN @Status IN (@Pending, @Running) THEN 1 ELSE 0 END,
                [ErrorMessage] = @ErrorMessage,
                [ShouldFail] = @ShouldFail
            WHERE [Id] = @Id;
            """;
        var command = new CommandDefinition(sql, new
        {
            process.Id,
            process.Progress,
            process.Status,
            process.ErrorMessage,
            process.ShouldFail,
            Pending = (EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Pending,
            Running = (EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Running,
        }, Transaction, cancellationToken: ctk);
        return await Connection.ExecuteAsync(command).ConfigureAwait(false) == 1;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
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

    private sealed class GreetingRow
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public NodaTime.LocalDate Date { get; set; }
        public NodaTime.LocalDateTime DateTime { get; set; }
        public NodaTime.OffsetDateTime OffsetDateTime { get; set; }
        public string Period { get; set; } = string.Empty;
        public Guid AuditId { get; set; }
        public byte[] ETag { get; set; } = [];

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
                ETag = "0x" + Convert.ToHexString(ETag),
            };
        }
    }

    private sealed class BookRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public EvolvableEnum<Book.V1.Genre> Genre { get; set; }
        public string? ISBN { get; set; }
        public string Description { get; set; } = string.Empty;

        public Book.V1.Output ToResponse()
        {
            return new Book.V1.Output
            {
                Id = Id,
                Title = Title,
                Author = Author,
                Genre = Genre,
                ISBN = ISBN,
                Description = Description,
            };
        }
    }
}

/// <summary>Creates transactional sample SQL contexts.</summary>
public sealed class SampleDataContextFactory :
    Ark.Tools.Sql.AbstractSqlAsyncContextFactory<SampleDataContext, SampleDataContext>,
    Ark.Tools.Outbox.IOutboxAsyncContextFactory,
    ISampleDataContextFactory
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

    async Task<ISampleDataContext> ISampleDataContextFactory.CreateAsync(CancellationToken ctk)
    {
        return await CreateAsync(ctk).ConfigureAwait(false);
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
    public async Task<GreetingResponse> SaveAndPublishAsync(GreetingResponse greeting, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        await context.SaveAsync(greeting, ctk).ConfigureAwait(false);
        var persisted = await context.ReadAsync(greeting.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Greeting '{greeting.Id}' was not found.");
        using var scope = _bus.Enlist(context);
        await _bus.Send(new GreetingCreatedNotification { Greeting = persisted }).ConfigureAwait(false);
        await scope.CompleteAsync().ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return persisted;
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
    public async Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var result = await context.ReadGreetingsAsync(query, ctk).ConfigureAwait(false);
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
    public async Task<GreetingResponse> UpdateAsync(Guid id, string message, string? expectedETag, AuditEntry? audit = null, CancellationToken ctk = default)
    {
        if (expectedETag is null || !IsValidETag(expectedETag))
            throw new Ark.Tools.Core.EntityTag.EntityTagMismatchException("The greeting ETag did not match.");

        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var auditId = audit?.Id ?? Guid.NewGuid();
        var updated = await context.UpdateAsync(id, message, expectedETag, auditId, ctk).ConfigureAwait(false);
        if (updated is null)
        {
            var exists = await context.ReadAsync(id, ctk).ConfigureAwait(false);
            if (exists is null)
                throw new EntityNotFoundException($"Greeting '{id}' was not found.");
            throw new Ark.Tools.Core.EntityTag.EntityTagMismatchException("The greeting ETag did not match.");
        }
        if (audit is not null)
            await context.WriteAuditAsync(audit, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return updated;
    }

    private static bool IsValidETag(string eTag)
    {
        if (!eTag.StartsWith("0x", StringComparison.Ordinal) || eTag.Length != 18)
            return false;

        try
        {
            return Convert.FromHexString(eTag.AsSpan(2)).Length == 8;
        }
        catch (FormatException)
        {
            return false;
        }
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
