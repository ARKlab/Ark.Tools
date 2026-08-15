// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox.SqlServer;
using Ark.Tools.Outbox;
using Ark.Tools.Core;

using Dapper;

using System.Data.Common;

namespace Ark.MediatorFramework.Sample.Application.DAL;

/// <summary>Composes fine-grained Book and audit operations in one application transaction.</summary>
public interface ISampleDataContext : IOutboxAsyncContext
{
    /// <summary>Gets the transactional outbox context for the current data transaction.</summary>
    IOutboxContextCore OutboxContext { get; }

    /// <summary>Writes an audit entry.</summary>
    Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default);

    /// <summary>Reads audit records.</summary>
    Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default);

    /// <summary>Commits the transaction.</summary>
    new Task CommitAsync(CancellationToken ctk = default);

    /// <summary>Saves a book and returns the persisted entity.</summary>
    Task<Book.V1.Output> SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default);

    /// <summary>Saves multiple books and returns the persisted entities.</summary>
    Task<IEnumerable<Book.V1.Output>> BulkInsertBooksAsync(
        IEnumerable<Book.V1.Output> books,
        CancellationToken ctk = default);

    /// <summary>Reads a book.</summary>
    Task<Book.V1.Output?> ReadBookAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Updates a book.</summary>
    Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default);

    /// <summary>Deletes a book.</summary>
    Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default);

    /// <summary>Reads a page of books.</summary>
    Task<Book.V1.Page> ReadBooksAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default);

    /// <summary>Saves a book review.</summary>
    Task SaveBookReviewAsync(BookReview review, CancellationToken ctk = default);

    /// <summary>Reads bounded reviews for a book.</summary>
    Task<IReadOnlyList<BookReview>> ReadBookReviewsAsync(Guid bookId, int skip, int limit, CancellationToken ctk = default);

    /// <summary>Saves reading activity.</summary>
    Task SaveReadingActivityAsync(ReadingActivity activity, CancellationToken ctk = default);

    /// <summary>Reads bounded activity for a book and reader.</summary>
    Task<IReadOnlyList<ReadingActivity>> ReadReadingActivityAsync(
        Guid bookId,
        string userId,
        int limit,
        CancellationToken ctk = default);

    /// <summary>Saves a book print process when no active process exists for the book.</summary>
    Task<bool> TrySaveBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default);

    /// <summary>Reads a book print process.</summary>
    Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(Guid id, bool forUpdate = false, CancellationToken ctk = default);

    /// <summary>Updates a book print process.</summary>
    Task<bool> UpdateBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default);

    /// <summary>Cancels a pending or running book print process.</summary>
    Task<BookPrintProcessResponse?> CancelBookPrintProcessAsync(Guid id, CancellationToken ctk = default);
}

/// <summary>Creates application contexts for handler-owned transactions.</summary>
public interface ISampleDataContextFactory : IOutboxAsyncContextFactory
{
    /// <summary>Creates a context.</summary>
    new Task<ISampleDataContext> CreateAsync(CancellationToken ctk = default);
}

/// <summary>SQL configuration used by the mediator sample.</summary>
public sealed class SampleDataContextConfig : IOutboxContextSqlConfig, Tools.Sql.ISqlContextConfig
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

/// <summary>Transactional SQL context for Books and Rebus outbox messages.</summary>
public sealed class SampleDataContext : AbstractSqlAsyncContextWithOutbox<SampleDataContext>, ISampleDataContext
{
    /// <inheritdoc />
    public IOutboxContextCore OutboxContext => this;

    /// <summary>Initializes a new instance of the <see cref="SampleDataContext"/> class.</summary>
    /// <param name="transaction">The transaction to use.</param>
    /// <param name="config">The SQL and outbox configuration.</param>
    public SampleDataContext(DbTransaction transaction, IOutboxContextSqlConfig config)
        : base(transaction, config)
    {
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
        var orderBy = _buildAuditOrderBy(query.Sort ?? []);
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
        var results = await Connection.QueryMultipleAsync(command).ConfigureAwait(false);
        await using var __ctx = results.ConfigureAwait(false);
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

    /// <summary>Saves a book in the current transaction.</summary>
    public async Task<Book.V1.Output> SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default)
    {
        const string sql = """
            INSERT INTO [dbo].[Book] ([Id], [Title], [Author], [Genre], [ISBN], [Description])
            OUTPUT INSERTED.[Id], INSERTED.[Title], INSERTED.[Author], INSERTED.[Genre],
                   INSERTED.[ISBN], INSERTED.[Description], INSERTED.[RowVersion] AS [ETag]
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
        var row = await Connection.QuerySingleAsync<BookRow>(command).ConfigureAwait(false);
        return row.ToResponse();
    }

    /// <summary>Saves multiple books in the current transaction using a table-valued parameter.</summary>
    public async Task<IEnumerable<Book.V1.Output>> BulkInsertBooksAsync(
        IEnumerable<Book.V1.Output> books,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(books);
        var rows = books.Select(static book => new BookBulkInsertRow(
            book.Id,
            book.Title,
            book.Author,
            book.Genre,
            book.ISBN,
            book.Description));
        var parameters = new
        {
            Books = rows.ToDataTableArk().AsTableValuedParameter("dbo.BookBulkInsertType"),
        };
        const string sql = """
            INSERT INTO [dbo].[Book]
            (
                [Id],
                [Title],
                [Author],
                [Genre],
                [ISBN],
                [Description]
            )
            OUTPUT
                INSERTED.[Id],
                INSERTED.[Title],
                INSERTED.[Author],
                INSERTED.[Genre],
                INSERTED.[ISBN],
                INSERTED.[Description],
                INSERTED.[RowVersion] AS [ETag]
            SELECT
                [Id],
                [Title],
                [Author],
                [Genre],
                [ISBN],
                [Description]
            FROM @Books;
            """;
        var command = new CommandDefinition(
            sql,
            parameters,
            Transaction,
            cancellationToken: ctk);
        var data = await Connection.QueryAsync<BookRow>(command).ConfigureAwait(false);
        return data.Select(static row => row.ToResponse()).ToArray();
    }

    /// <summary>Reads a book by identifier in the current transaction.</summary>
    public async Task<Book.V1.Output?> ReadBookAsync(
        Guid id,
        CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [Title], [Author], [Genre], [ISBN], [Description], [RowVersion] AS [ETag]
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
            WHERE [Id] = @Id
              AND [RowVersion] = TRY_CONVERT(VARBINARY(8), @ETag, 1);
            """;
        var command = new CommandDefinition(sql, new
        {
            book.Id,
            book.Title,
            book.Author,
            book.Genre,
            book.ISBN,
            book.Description,
            book.ETag,
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
        var orderBy = _buildBookOrderBy(query.Sort ?? []);
        var sql = $"""
            SELECT [Id], [Title], [Author], [Genre], [ISBN], [Description], [RowVersion] AS [ETag]
            FROM [dbo].[Book]
            WHERE (@Title IS NULL OR [Title] = @Title)
              AND (@Author IS NULL OR [Author] = @Author)
              AND (@Genre IS NULL OR [Genre] = @Genre)
            ORDER BY {orderBy}
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
        var results = await Connection.QueryMultipleAsync(command).ConfigureAwait(false);
        await using var __ctx = results.ConfigureAwait(false);
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

    /// <summary>Saves a book review in the current transaction.</summary>
    public async Task SaveBookReviewAsync(BookReview review, CancellationToken ctk = default)
    {
        const string sql = """
            INSERT INTO [dbo].[BookReview] ([Id], [BookId], [UserId], [Rating], [Text], [CreatedAt])
            VALUES (@Id, @BookId, @UserId, @Rating, @Text, @CreatedAt);
            """;
        var command = new CommandDefinition(sql, review, Transaction, cancellationToken: ctk);
        await Connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <summary>Reads bounded reviews for a book in the current transaction.</summary>
    public async Task<IReadOnlyList<BookReview>> ReadBookReviewsAsync(
        Guid bookId,
        int skip,
        int limit,
        CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [BookId], [UserId], [Rating], [Text], [CreatedAt]
            FROM [dbo].[BookReview]
            WHERE [BookId] = @BookId
            ORDER BY [CreatedAt] DESC, [Id] DESC
            OFFSET @Skip ROWS FETCH NEXT @Limit ROWS ONLY;
            """;
        var command = new CommandDefinition(sql, new { BookId = bookId, Skip = skip, Limit = limit }, Transaction, cancellationToken: ctk);
        var reviews = await Connection.QueryAsync<BookReview>(command).ConfigureAwait(false);
        return reviews.ToArray();
    }

    /// <summary>Saves reading activity in the current transaction.</summary>
    public async Task SaveReadingActivityAsync(ReadingActivity activity, CancellationToken ctk = default)
    {
        const string sql = """
            INSERT INTO [dbo].[ReadingActivity] ([Id], [BookId], [UserId], [Kind], [Progress], [OccurredAt])
            VALUES (@Id, @BookId, @UserId, @Kind, @Progress, @OccurredAt);
            """;
        var command = new CommandDefinition(sql, new
        {
            activity.Id,
            activity.BookId,
            activity.UserId,
            activity.Kind,
            activity.Progress,
            activity.OccurredAt,
        }, Transaction, cancellationToken: ctk);
        await Connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <summary>Reads bounded reading activity for a book and reader in the current transaction.</summary>
    public async Task<IReadOnlyList<ReadingActivity>> ReadReadingActivityAsync(
        Guid bookId,
        string userId,
        int limit,
        CancellationToken ctk = default)
    {
        const string sql = """
            SELECT [Id], [BookId], [UserId], [Kind], [Progress], [OccurredAt]
            FROM [dbo].[ReadingActivity]
            WHERE [BookId] = @BookId AND [UserId] = @UserId
            ORDER BY [OccurredAt] DESC, [Id] DESC
            OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY;
            """;
        var command = new CommandDefinition(sql, new { BookId = bookId, UserId = userId, Limit = limit }, Transaction, cancellationToken: ctk);
        var activities = await Connection.QueryAsync<ReadingActivity>(command).ConfigureAwait(false);
        return activities.ToArray();
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
            Pending = BookPrintProcessStatus.Pending.ToEvolvable(),
            Running = BookPrintProcessStatus.Running.ToEvolvable(),
        }, Transaction, cancellationToken: ctk);
        return await Connection.ExecuteAsync(command).ConfigureAwait(false) == 1;
    }

    /// <summary>Reads a book print process in the current transaction.</summary>
    public async Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(
        Guid id,
        bool forUpdate = false,
        CancellationToken ctk = default)
    {
        const string sqlWithoutLock = """
            SELECT [Id], [BookId], [Progress], [Status], [ErrorMessage], [ShouldFail]
            FROM [dbo].[BookPrintProcess]
            WHERE [Id] = @Id;
            """;
        const string sqlWithLock = """
            SELECT [Id], [BookId], [Progress], [Status], [ErrorMessage], [ShouldFail]
            FROM [dbo].[BookPrintProcess] WITH (UPDLOCK, HOLDLOCK)
            WHERE [Id] = @Id;
            """;
        var sql = forUpdate ? sqlWithLock : sqlWithoutLock;
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
            WHERE [Id] = @Id
              AND
              (
                  (@Status = @Running AND [Status] = @Pending)
                  OR (@Status = @Completed AND [Status] = @Running)
                  OR (@Status = @Error AND [Status] IN (@Running, @Completed))
              );
            """;
        var command = new CommandDefinition(sql, new
        {
            process.Id,
            process.Progress,
            process.Status,
            process.ErrorMessage,
            process.ShouldFail,
            Pending = BookPrintProcessStatus.Pending.ToEvolvable(),
            Running = BookPrintProcessStatus.Running.ToEvolvable(),
            Completed = BookPrintProcessStatus.Completed.ToEvolvable(),
            Error = BookPrintProcessStatus.Error.ToEvolvable(),
        }, Transaction, cancellationToken: ctk);
        return await Connection.ExecuteAsync(command).ConfigureAwait(false) == 1;
    }

    /// <summary>Cancels a pending or running book print process in the current transaction.</summary>
    public async Task<BookPrintProcessResponse?> CancelBookPrintProcessAsync(Guid id, CancellationToken ctk = default)
    {
        const string sql = """
            UPDATE [dbo].[BookPrintProcess]
            SET [Status] = @Cancelled,
                [IsActive] = 0
            OUTPUT inserted.[Id], inserted.[BookId], inserted.[Progress], inserted.[Status],
                   inserted.[ErrorMessage], inserted.[ShouldFail]
            WHERE [Id] = @Id
              AND [Status] IN (@Pending, @Running);
            """;
        var command = new CommandDefinition(sql, new
        {
            Id = id,
            Cancelled = BookPrintProcessStatus.Cancelled.ToEvolvable(),
            Pending = BookPrintProcessStatus.Pending.ToEvolvable(),
            Running = BookPrintProcessStatus.Running.ToEvolvable(),
        }, Transaction, cancellationToken: ctk);
        return await Connection.QuerySingleOrDefaultAsync<BookPrintProcessResponse>(command).ConfigureAwait(false);
    }

    private static string _escapeLikePattern(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal)
            .Replace("[", @"\[", StringComparison.Ordinal);
    }

    private static string _buildAuditOrderBy(IEnumerable<string> sorts)
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

    private static string _buildBookOrderBy(IEnumerable<string> sorts)
    {
        var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(Book.V1.Output.Id)] = "[Id]",
            [nameof(Book.V1.Output.Title)] = "[Title]",
            [nameof(Book.V1.Output.Author)] = "[Author]",
            [nameof(Book.V1.Output.Genre)] = "[Genre]",
            [nameof(Book.V1.Output.ISBN)] = "[ISBN]",
            [nameof(Book.V1.Output.Description)] = "[Description]",
        };
        var orderBy = sorts
            .Where(sort => !string.IsNullOrWhiteSpace(sort))
            .Select(sort =>
            {
                var parts = sort.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2 || !columns.TryGetValue(parts[0], out var column))
                    throw new ArgumentException($"Invalid book sort '{sort}'.", nameof(sorts));
                var direction = parts.Length == 2
                    ? parts[1].ToUpperInvariant() switch
                    {
                        "ASC" => " ASC",
                        "DESC" => " DESC",
                        _ => throw new ArgumentException($"Invalid book sort direction '{parts[1]}'.", nameof(sorts)),
                    }
                    : string.Empty;
                return column + direction;
            })
            .ToArray();
        return orderBy.Length == 0 ? "[Id]" : string.Join(", ", orderBy);
    }

    private sealed record BookBulkInsertRow(
        Guid Id,
        string Title,
        string Author,
        EvolvableEnum<Book.V1.Genre> Genre,
        string? ISBN,
        string Description);

    private sealed class BookRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public EvolvableEnum<Book.V1.Genre> Genre { get; set; }
        public string? ISBN { get; set; }
        public string Description { get; set; } = string.Empty;
        public byte[] ETag { get; set; } = [];

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
                ETag = "0x" + Convert.ToHexString(ETag),
            };
        }
    }
}

/// <summary>Creates transactional sample SQL contexts.</summary>
public sealed class SampleDataContextFactory :
    Tools.Sql.AbstractSqlAsyncContextFactory<SampleDataContext, SampleDataContext>,
    IOutboxAsyncContextFactory,
    ISampleDataContextFactory
{
    private readonly SampleDataContextConfig _config;

    /// <summary>Initializes a new instance of the <see cref="SampleDataContextFactory"/> class.</summary>
    /// <param name="connectionManager">The SQL connection manager.</param>
    /// <param name="config">The sample database configuration.</param>
    public SampleDataContextFactory(Tools.Sql.IDbConnectionManager connectionManager, SampleDataContextConfig config)
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

    async Task<IOutboxAsyncContext> IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
    {
        return await CreateAsync(ctk).ConfigureAwait(false);
    }
}
