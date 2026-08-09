// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
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

        var created = await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest { Name = "etag" }).ConfigureAwait(false);
        var updated = await context.DispatchRequestAsync<UpdateGreetingMessageRequest, GreetingResponse>(
            new UpdateGreetingMessageRequest
            {
                Id = created.Id,
                Message = "updated once",
                ETag = created.ETag,
            }).ConfigureAwait(false);

        updated.ETag.Should().NotBe(created.ETag);
        var stale = () => context.DispatchRequestAsync<UpdateGreetingMessageRequest, GreetingResponse>(
            new UpdateGreetingMessageRequest
            {
                Id = created.Id,
                Message = "stale",
                ETag = created.ETag,
            });

        (await stale.Should().ThrowAsync<EntityTagMismatchException>().ConfigureAwait(false))
            .Which.Message.Should().Contain("ETag");
    }

    /// <summary>Retries transient store failures before completing an update.</summary>
    [TestMethod]
    public async Task DirectRoundtripRetriesTransientFailures()
    {
        var faults = new ConcurrencyFaultInjector { PendingFailures = 2 };
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var decoratedFactory = new FaultInjectingSampleDataContextFactory(factory, faults);
        await using var context = new ApplicationTestContext(
            useSqlStore: false,
            dataContextFactory: decoratedFactory);

        var created = await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest { Name = "retry" }).ConfigureAwait(false);
        var updated = await context.DispatchRequestAsync<UpdateGreetingMessageRequest, GreetingResponse>(
            new UpdateGreetingMessageRequest
            {
                Id = created.Id,
                Message = "retried",
                ETag = created.ETag,
            }).ConfigureAwait(false);

        updated.Message.Should().Be("retried");
        faults.PendingFailures.Should().Be(0);
    }
}
