// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies transactional behavior of the in-memory outbox profile.</summary>
[TestClass]
public sealed class InMemoryOutboxTests
{
    /// <summary>Stages messages until the context commits.</summary>
    [TestMethod]
    public async Task MessagesAreVisibleAfterCommit()
    {
        var factory = new InMemoryOutboxContextFactory();
        await using (var writer = await factory.CreateAsync().ConfigureAwait(false))
        {
            await writer.SendAsync(
                [
                    new OutboxMessage
                    {
                        Headers = new Dictionary<string, string>(StringComparer.Ordinal) { ["type"] = "test" },
                        Body = [1, 2, 3],
                    },
                ]).ConfigureAwait(false);

            await using var beforeCommit = await factory.CreateAsync().ConfigureAwait(false);
            (await beforeCommit.CountAsync().ConfigureAwait(false)).Should().Be(0);
            await writer.CommitAsync().ConfigureAwait(false);
        }

        await using var reader = await factory.CreateAsync().ConfigureAwait(false);
        (await reader.CountAsync().ConfigureAwait(false)).Should().Be(1);
    }

    /// <summary>Releases peek locks when a processor context is disposed without commit.</summary>
    [TestMethod]
    public async Task PeekLockIsReleasedWithoutCommit()
    {
        var factory = new InMemoryOutboxContextFactory();
        await using (var writer = await factory.CreateAsync().ConfigureAwait(false))
        {
            await writer.SendAsync([new OutboxMessage { Body = [4] }]).ConfigureAwait(false);
            await writer.CommitAsync().ConfigureAwait(false);
        }

        await using (var abandoned = await factory.CreateAsync().ConfigureAwait(false))
        {
            (await abandoned.PeekLockMessagesAsync().ConfigureAwait(false)).Should().HaveCount(1);
        }

        await using var retry = await factory.CreateAsync().ConfigureAwait(false);
        (await retry.PeekLockMessagesAsync().ConfigureAwait(false)).Should().HaveCount(1);
        await retry.CommitAsync().ConfigureAwait(false);

        await using var empty = await factory.CreateAsync().ConfigureAwait(false);
        (await empty.CountAsync().ConfigureAwait(false)).Should().Be(0);
    }

    /// <summary>Counts staged messages and clears them with the transaction.</summary>
    [TestMethod]
    public async Task ClearRemovesStagedAndCommittedMessages()
    {
        var factory = new InMemoryOutboxContextFactory();
        await using (var writer = await factory.CreateAsync().ConfigureAwait(false))
        {
            await writer.SendAsync([new OutboxMessage { Body = [5] }]).ConfigureAwait(false);
            (await writer.CountAsync().ConfigureAwait(false)).Should().Be(1);
            await writer.CommitAsync().ConfigureAwait(false);
        }

        await using (var clearer = await factory.CreateAsync().ConfigureAwait(false))
        {
            await clearer.SendAsync([new OutboxMessage { Body = [6] }]).ConfigureAwait(false);
            (await clearer.CountAsync().ConfigureAwait(false)).Should().Be(2);
            await clearer.ClearAsync().ConfigureAwait(false);
            (await clearer.CountAsync().ConfigureAwait(false)).Should().Be(0);
            await clearer.CommitAsync().ConfigureAwait(false);
        }

        await using var empty = await factory.CreateAsync().ConfigureAwait(false);
        (await empty.CountAsync().ConfigureAwait(false)).Should().Be(0);
    }
}
