// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AotSample.Core.API;
using Ark.Tools.Outbox;

using Dapper;

using Microsoft.Data.SqlClient;

using NodaTime;

using System.Data;
using System.Text.Json;

[module: DapperAot]

namespace Ark.MediatorFramework.AotSample.Core.Application.DAL;

/// <summary>One SQL Server transaction: the books and the outbox the bus enlists in.</summary>
/// <remarks>
/// Dapper.AOT intercepts every Dapper call in this project, so data access needs no reflection. The sample implements the
/// outbox itself because <c>Ark.Tools.Outbox.SqlServer</c> calls Dapper from its own assembly, which Dapper.AOT does not
/// intercept.
/// </remarks>
public sealed class AotDataContext : IOutboxAsyncContext
{
    private readonly SqlConnection _connection;
    private SqlTransaction _transaction;

    private AotDataContext(SqlConnection connection, SqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>Opens a connection and begins a transaction.</summary>
    /// <param name="sql">The SQL Server connection string.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The context.</returns>
    public static async Task<AotDataContext> OpenAsync(string sql, CancellationToken ctk = default)
    {
        var connection = new SqlConnection(sql);
        try
        {
            await connection.OpenAsync(ctk).ConfigureAwait(false);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ctk).ConfigureAwait(false);
            return new AotDataContext(connection, transaction);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private const string _bookColumns = "[Id], [Title], [Genre], [PublishedOn], [ReleasedAt], [PrintSlot], [CreatedAt], [PrintedAt], [Contact], [Printed]";

    /// <summary>Inserts a book.</summary>
    /// <param name="book">The book.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task InsertBookAsync(Book book, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        await _connection.ExecuteAsync(
            """
            INSERT INTO [dbo].[Book] ([Id], [Title], [Genre], [PublishedOn], [ReleasedAt], [PrintSlot], [CreatedAt], [Contact])
            VALUES (@Id, @Title, @Genre, @PublishedOn, @ReleasedAt, @PrintSlot, @CreatedAt, @Contact)
            """,
            new { book.Id, book.Title, book.Genre, book.PublishedOn, book.ReleasedAt, book.PrintSlot, book.CreatedAt, book.Contact, ctk },
            _transaction).ConfigureAwait(false);
    }

    /// <summary>Reads a book.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The book, or <see langword="null"/>.</returns>
    public async Task<Book?> ReadBookAsync(BookId id, CancellationToken ctk = default)
    {
        return await _connection.QuerySingleOrDefaultAsync<Book>(
            "SELECT " + _bookColumns + " FROM [dbo].[Book] WHERE [Id] = @id",
            new { id, ctk },
            _transaction).ConfigureAwait(false);
    }

    /// <summary>Marks a book printed.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="printedAt">When it was printed, in UTC.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns><see langword="true"/> when the book exists.</returns>
    public async Task<bool> MarkPrintedAsync(BookId id, LocalDateTime printedAt, CancellationToken ctk = default)
    {
        return await _connection.ExecuteAsync(
            "UPDATE [dbo].[Book] SET [Printed] = 1, [PrintedAt] = @printedAt WHERE [Id] = @id",
            new { id, printedAt, ctk },
            _transaction).ConfigureAwait(false) == 1;
    }

    /// <inheritdoc />
    public async Task SendAsync(IEnumerable<OutboxMessage> messages, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        foreach (var message in messages)
        {
            // Plain ADO.NET: Dapper.AOT 1.1.0 binds a byte[] parameter as a list expansion, which breaks the SQL.
            var command = _connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.Transaction = _transaction;
                command.CommandText = "INSERT INTO [dbo].[Outbox] ([Headers], [Body]) VALUES (@headers, @body)";
                command.Parameters.Add("@headers", SqlDbType.NVarChar, -1).Value =
                    JsonSerializer.Serialize(message.Headers ?? [], AotJsonContext.Default.DictionaryStringString);
                command.Parameters.Add("@body", SqlDbType.VarBinary, -1).Value = message.Body ?? [];
                await command.ExecuteNonQueryAsync(ctk).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OutboxMessage>> PeekLockMessagesAsync(int messageCount = 10, CancellationToken ctk = default)
    {
#pragma warning disable DAP223 // DELETE lacks WHERE: TOP in the CTE limits it to the batch.
        var rows = await _connection.QueryAsync<OutboxRow>(
            """
            ;WITH batch AS (SELECT TOP (@messageCount) * FROM [dbo].[Outbox] ORDER BY [Id])
            DELETE FROM batch WITH (READPAST, ROWLOCK, READCOMMITTEDLOCK)
            OUTPUT DELETED.[Headers], DELETED.[Body]
            """,
                new { messageCount, ctk },
                _transaction).ConfigureAwait(false);
#pragma warning restore DAP223
        return rows.Select(static row => new OutboxMessage
        {
            Headers = JsonSerializer.Deserialize(row.Headers, AotJsonContext.Default.DictionaryStringString),
            Body = row.Body,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken ctk = default)
    {
        return await _connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM [dbo].[Outbox]",
            new { ctk },
            _transaction).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken ctk = default)
    {
#pragma warning disable DAP223 // DELETE lacks WHERE: clearing the outbox is the point.
        await _connection.ExecuteAsync(
            "DELETE FROM [dbo].[Outbox]",
            new { ctk },
            _transaction).ConfigureAwait(false);
#pragma warning restore DAP223
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken ctk = default)
    {
        await _transaction.CommitAsync(ctk).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CommitAsync(bool reuse, CancellationToken ctk = default)
    {
        await _transaction.CommitAsync(ctk).ConfigureAwait(false);
        if (reuse)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(ctk).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    // Not private: the Dapper.AOT generated code materializes it.
    internal sealed class OutboxRow
    {
        public string Headers { get; set; } = string.Empty;

        public byte[] Body { get; set; } = [];
    }
}
