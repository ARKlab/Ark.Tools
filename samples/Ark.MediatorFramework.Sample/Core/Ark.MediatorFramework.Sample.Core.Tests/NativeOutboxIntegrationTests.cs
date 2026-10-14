// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Tests.Hooks;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;
using Ark.Tools.Outbox.SqlServer;

using AwesomeAssertions;

using Microsoft.Data.SqlClient;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Core.Tests;

/// <summary>Verifies the native outbox against the sample SQL and host composition.</summary>
[TestClass]
[TestCategory("integration")]
public sealed class NativeOutboxIntegrationTests
{
    [TestMethod]
    public async Task ApplicationStateAndOutboxCommitOrRollBackTogether()
    {
        if (_inMemoryProfile())
            Assert.Inconclusive("The SQL integration profile is disabled.");
        await DatabaseHooks.ResetDatabaseAsync().ConfigureAwait(false);
        var factory = _sqlFactory();
        var committedAuditId = AuditId.New();
        var committed = await factory.CreateAsync().ConfigureAwait(false);
        await using (var _ = committed.ConfigureAwait(false))
        {
            await committed.WriteAuditAsync(_audit(committedAuditId)).ConfigureAwait(false);
            await committed.SendAsync([_message(1)]).ConfigureAwait(false);
            await committed.CommitAsync().ConfigureAwait(false);
        }

        (await _countAsync("Audit").ConfigureAwait(false)).Should().Be(1);
        (await _countAsync("Outbox").ConfigureAwait(false)).Should().Be(1);

        var rolledBack = await factory.CreateAsync().ConfigureAwait(false);
        await using (var _ = rolledBack.ConfigureAwait(false))
        {
            await rolledBack.WriteAuditAsync(_audit(AuditId.New())).ConfigureAwait(false);
            await rolledBack.SendAsync([_message(2)]).ConfigureAwait(false);
        }

        (await _countAsync("Audit").ConfigureAwait(false)).Should().Be(1);
        (await _countAsync("Outbox").ConfigureAwait(false)).Should().Be(1);
    }

    [TestMethod]
    public async Task ConcurrentSqlPeekLocksSelectDifferentRowsAndRollbackReleasesThem()
    {
        if (_inMemoryProfile())
            Assert.Inconclusive("The SQL integration profile is disabled.");
        await DatabaseHooks.ResetDatabaseAsync().ConfigureAwait(false);
        var factory = _sqlFactory();
        var seed = await factory.CreateAsync().ConfigureAwait(false);
        await using (var __seed = seed.ConfigureAwait(false))
        {
            await seed.SendAsync([_message(1), _message(2)]).ConfigureAwait(false);
            await seed.CommitAsync().ConfigureAwait(false);
        }

        var first = await factory.CreateAsync().ConfigureAwait(false);
        await using var __first = first.ConfigureAwait(false);
        var firstMessage = (await first.PeekLockMessagesAsync(1).ConfigureAwait(false)).Single();
        var second = await factory.CreateAsync().ConfigureAwait(false);
        await using var __second = second.ConfigureAwait(false);
        var secondMessage = (await second.PeekLockMessagesAsync(1).ConfigureAwait(false)).Single();

        firstMessage.Body.Should().NotEqual(secondMessage.Body);
    }

    [TestMethod]
    public async Task SqlSendAsyncInsertsOneRoundTripPerChunkInInputOrder()
    {
        if (_inMemoryProfile())
            Assert.Inconclusive("The SQL integration profile is disabled.");
        await DatabaseHooks.ResetDatabaseAsync().ConfigureAwait(false);
        // 1500 messages span two 1000-row chunks.
        var messages = Enumerable.Range(0, 1500).Select(_indexedMessage).ToList();
        var connection = new SqlConnection(DatabaseHooks.ConnectionString);
        await using var __connection = connection.ConfigureAwait(false);
        connection.StatisticsEnabled = true;
        await connection.OpenAsync().ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);
        var context = new SampleDataContext(transaction, new SampleDataContextConfig(DatabaseHooks.ConnectionString));
        await using (var _ = context.ConfigureAwait(false))
        {
            connection.ResetStatistics();
            await context.SendAsync(messages).ConfigureAwait(false);
            var roundTrips = (long)connection.RetrieveStatistics()["ServerRoundtrips"]!;
            await context.CommitAsync().ConfigureAwait(false);

            roundTrips.Should().Be(2);
        }

        var stored = await _readOutboxInIdOrderAsync().ConfigureAwait(false);
        stored.Select(static x => BitConverter.ToInt32(x.Body)).Should().Equal(Enumerable.Range(0, 1500));
        stored.Select(static x => x.Headers!["index"]).Should().Equal(
            Enumerable.Range(0, 1500).Select(static i => i.ToString(CultureInfo.InvariantCulture)));
    }

    private static ISampleDataContextFactory _sqlFactory()
    {
        SampleDapperMappings.Register();
        return new SampleDataContextFactory(
            new Ark.Tools.Sql.SqlServer.SqlConnectionManager(),
            new SampleDataContextConfig(DatabaseHooks.ConnectionString));
    }

    private static AuditEntry _audit(AuditId id)
    {
        return new AuditEntry
        {
            Id = id,
            UserId = UserId.From("native-outbox-test"),
            EntityType = "NativeOutbox",
            Identifier = id.Value.ToString("D"),
            Operation = "enqueue",
            Timestamp = SystemClock.Instance.GetCurrentInstant(),
        };
    }

    private static OutboxMessage _message(byte value)
    {
        return new OutboxMessage
        {
            Body = [value],
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MessagingHeaders.MessageId] = Guid.NewGuid().ToString("N"),
                [MessagingHeaders.OutboxDestinationKind] = "queue",
                [MessagingHeaders.OutboxDestination] = "processor",
            },
        };
    }

    private static OutboxMessage _indexedMessage(int index)
    {
        return new OutboxMessage
        {
            Body = BitConverter.GetBytes(index),
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["index"] = index.ToString(CultureInfo.InvariantCulture),
            },
        };
    }

    private static async Task<List<OutboxMessage>> _readOutboxInIdOrderAsync()
    {
        var connection = new SqlConnection(DatabaseHooks.ConnectionString);
        await using var __connection = connection.ConfigureAwait(false);
        await connection.OpenAsync().ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var __command = command.ConfigureAwait(false);
        command.CommandText = "SELECT [Headers], [Body] FROM [dbo].[Outbox] ORDER BY [Id]";
        var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        await using var __reader = reader.ConfigureAwait(false);
        var serializer = new HeaderSerializer();
        var rows = new List<OutboxMessage>();
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            rows.Add(new OutboxMessage
            {
                Headers = serializer.DeserializeFromString(reader.GetString(0)),
                Body = (byte[])reader[1],
            });
        }

        return rows;
    }

    private static async Task<int> _countAsync(string table)
    {
        var connection = new SqlConnection(DatabaseHooks.ConnectionString);
        await using var __connection = connection.ConfigureAwait(false);
        await connection.OpenAsync().ConfigureAwait(false);
        var command = connection.CreateCommand();
        await using var __command = command.ConfigureAwait(false);
#pragma warning disable CA2100 // The table name is selected from this fixed allow-list.
        command.CommandText = table switch
        {
            "Audit" => "SELECT COUNT(*) FROM [dbo].[Audit]",
            "Outbox" => "SELECT COUNT(*) FROM [dbo].[Outbox]",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
#pragma warning restore CA2100
        return Convert.ToInt32(
            await command.ExecuteScalarAsync().ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private static bool _inMemoryProfile()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("ARK_SAMPLE_INMEMORY_TESTS"),
            "1",
            StringComparison.Ordinal);
    }
}
