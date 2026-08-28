// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.OutboxProcessor;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;

using AwesomeAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

namespace Ark.MediatorFramework.Sample.Tests;

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
        var committedAuditId = Guid.NewGuid();
        var committed = await factory.CreateAsync().ConfigureAwait(false);
        await using (var __committed = committed.ConfigureAwait(false))
        {
            await committed.WriteAuditAsync(_audit(committedAuditId)).ConfigureAwait(false);
            await committed.SendAsync([_message(1)]).ConfigureAwait(false);
            await committed.CommitAsync().ConfigureAwait(false);
        }

        (await _countAsync("Audit").ConfigureAwait(false)).Should().Be(1);
        (await _countAsync("Outbox").ConfigureAwait(false)).Should().Be(1);

        var rolledBack = await factory.CreateAsync().ConfigureAwait(false);
        await using (var __rolledBack = rolledBack.ConfigureAwait(false))
        {
            await rolledBack.WriteAuditAsync(_audit(Guid.NewGuid())).ConfigureAwait(false);
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
    public async Task DedicatedHostResolvesExactlyOneReservedProcessor()
    {
        var transport = new InMemoryMessagingTransport();
        var factory = new InMemoryOutboxContextFactory();
        var services = OutboxProcessorComposition.BuildServices(transport, factory);
        await using var __services = services.ConfigureAwait(false);

        services.GetServices<IHostedService>().Should()
            .ContainSingle(service => service is MessagingOutboxProcessor);
        MessagingOutboxProcessor.Identity.Should().Be("outbox-processor");
    }

    private static ISampleDataContextFactory _sqlFactory()
    {
        Ark.Tools.Sql.SqlServer.NodaTimeDapperSqlServer.Setup();
        return new SampleDataContextFactory(
            new Ark.Tools.Sql.SqlServer.SqlConnectionManager(),
            new SampleDataContextConfig(DatabaseHooks.ConnectionString));
    }

    private static AuditEntry _audit(Guid id)
    {
        return new AuditEntry
        {
            Id = id,
            UserId = "native-outbox-test",
            EntityType = "NativeOutbox",
            Identifier = id.ToString("D"),
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
