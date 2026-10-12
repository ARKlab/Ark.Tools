// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Outbox;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the ambient outbox scope lifecycle.</summary>
[TestClass]
public sealed class OutboxScopeTests
{
    [TestMethod]
    public async Task NestedScopeRestoresTheEnclosingOneOnDispose()
    {
        var context = await new InMemoryOutboxContextFactory().CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);

        using var outer = new OutboxScope(context);
        using (var inner = new OutboxScope(context))
            OutboxScope.Current.Should().BeSameAs(inner);

        OutboxScope.Current.Should().BeSameAs(outer);
    }

    [TestMethod]
    public async Task CompletedScopeRejectsMoreMessagesAndASecondCompletion()
    {
        var context = await new InMemoryOutboxContextFactory().CreateAsync().ConfigureAwait(false);
        await using var __context = context.ConfigureAwait(false);
        using var scope = new OutboxScope(context);
        scope.Add(new OutboxMessage { Headers = [], Body = [1] });

        await scope.CompleteAsync().ConfigureAwait(false);

        (await context.CountAsync().ConfigureAwait(false)).Should().Be(1);
        var add = () => scope.Add(new OutboxMessage { Headers = [], Body = [2] });
        add.Should().Throw<InvalidOperationException>();
        var complete = async () => await scope.CompleteAsync().ConfigureAwait(false);
        await complete.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
    }
}
