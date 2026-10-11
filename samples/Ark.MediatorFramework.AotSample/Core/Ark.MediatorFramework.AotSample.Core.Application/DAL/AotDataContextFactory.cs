// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using Dapper;

using Microsoft.Data.SqlClient;

namespace Ark.MediatorFramework.AotSample.Core.Application.DAL;

/// <summary>Opens <see cref="AotDataContext"/> instances; the outbox processor uses it too.</summary>
/// <param name="sql">The SQL Server connection string.</param>
public sealed class AotDataContextFactory(string sql) : IOutboxAsyncContextFactory
{
    /// <summary>Opens a context with a new transaction.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The context.</returns>
    public async Task<AotDataContext> OpenAsync(CancellationToken ctk = default)
    {
        return await AotDataContext.OpenAsync(sql, ctk).ConfigureAwait(false);
    }

    async Task<IOutboxAsyncContext> IOutboxAsyncContextFactory.CreateAsync(CancellationToken ctk)
    {
        return await OpenAsync(ctk).ConfigureAwait(false);
    }

    /// <summary>Creates the database and its tables when they are missing.</summary>
    /// <remarks>shortcut: the sample has no database project; deploy the schema with your usual tooling instead.</remarks>
    /// <param name="ctk">The cancellation token.</param>
    public async Task EnsureCreatedAsync(CancellationToken ctk = default)
    {
        var builder = new SqlConnectionStringBuilder(sql);
        var database = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using (var master = new SqlConnection(builder.ConnectionString))
        {
            await master.OpenAsync(ctk).ConfigureAwait(false);
            await master.ExecuteAsync("""
                IF DB_ID(@database) IS NULL
                BEGIN
                    DECLARE @create NVARCHAR(300) = N'CREATE DATABASE ' + QUOTENAME(@database);
                    EXEC sp_executesql @create;
                END
                """, new { database, ctk }).ConfigureAwait(false);
        }

        await using (var connection = new SqlConnection(sql))
        {
            await connection.OpenAsync(ctk).ConfigureAwait(false);
            await connection.ExecuteAsync(_schema, new { ctk }).ConfigureAwait(false);
        }
    }

    private const string _schema = """
        IF OBJECT_ID('[dbo].[Book]') IS NULL
            CREATE TABLE [dbo].[Book] (
                [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK_Book] PRIMARY KEY,
                [Title] NVARCHAR(200) NOT NULL,
                [Genre] NVARCHAR(50) NOT NULL,
                [PublishedOn] DATE NOT NULL,
                [ReleasedAt] DATETIMEOFFSET NOT NULL,
                [PrintSlot] TIME NOT NULL,
                [CreatedAt] DATETIME2 NOT NULL,
                [PrintedAt] DATETIME2 NULL,
                [Contact] NVARCHAR(320) NOT NULL,
                [Printed] BIT NOT NULL CONSTRAINT [DF_Book_Printed] DEFAULT (0));
        IF OBJECT_ID('[dbo].[Outbox]') IS NULL
            CREATE TABLE [dbo].[Outbox] (
                [Id] BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Outbox] PRIMARY KEY,
                [Headers] NVARCHAR(MAX) NOT NULL,
                [Body] VARBINARY(MAX) NOT NULL);
        """;
}
