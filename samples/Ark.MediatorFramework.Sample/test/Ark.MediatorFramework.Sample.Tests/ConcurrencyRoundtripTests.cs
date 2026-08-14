// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.


using Ark.Tools.Outbox;
using Ark.MediatorFramework.Sample.Tests.Fakes;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.Tools.Core.EntityTag;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies optimistic concurrency through application contracts.</summary>
[TestClass]
public sealed class ConcurrencyRoundtripTests
{
    /// <summary>Updates with a current ETag and rejects a stale ETag.</summary>
    [TestMethod]
    public async Task DirectRoundtripUsesAndRejectsStaleETags()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("concurrency-user");

        var created = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "etag" })).ConfigureAwait(false);
        var updated = await context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(
            new Greeting_UpdateRequest.V1(
                new Greeting.V1.Input { Message = "updated once" },
                created.Id,
                created.ETag)).ConfigureAwait(false);

        updated.ETag.Should().NotBe(created.ETag);
        var stale = () => context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(
            new Greeting_UpdateRequest.V1(
                new Greeting.V1.Input { Message = "stale" },
                created.Id,
                created.ETag));

        (await stale.Should().ThrowAsync<EntityTagMismatchException>().ConfigureAwait(false))
            .Which.Message.Should().Contain("ETag");
    }

    /// <summary>Retries transient context failures before completing an update.</summary>
    [TestMethod]
    public async Task DirectRoundtripRetriesTransientFailures()
    {
        var faults = new ConcurrencyFaultInjector { PendingFailures = 2 };
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var decoratedFactory = new FaultInjectingSampleDataContextFactory(factory, faults);
        await using var context = new ApplicationTestContext(
            useSqlStore: false,
            dataContextFactory: decoratedFactory);

        var created = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "retry" })).ConfigureAwait(false);
        var updated = await context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(
            new Greeting_UpdateRequest.V1(
                new Greeting.V1.Input { Message = "retried" },
                created.Id,
                created.ETag)).ConfigureAwait(false);

        updated.Message.Should().Be("retried");
        faults.PendingFailures.Should().Be(0);
    }
}
