// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.Tools.Core;
using Ark.Tools.Solid;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies optimistic concurrency behavior used by the sample.</summary>
[TestClass]
public sealed class OptimisticConcurrencyTests
{
    /// <summary>Retries transient optimistic conflicts before returning the handler result.</summary>
    [TestMethod]
    public async Task RetrierRetriesOptimisticConflict()
    {
        var inner = new ConflictHandler(2);
        var handler = new OptimisticConcurrencyRetrierDecorator<CreateGreetingRequest, GreetingResponse>(inner);

        var result = await handler.ExecuteAsync(new CreateGreetingRequest()).ConfigureAwait(false);

        result.Message.Should().Be("ok");
        inner.Attempts.Should().Be(3);
    }

    /// <summary>Stops retrying after the configured retry budget is exhausted.</summary>
    [TestMethod]
    public async Task RetrierPropagatesExhaustedConflict()
    {
        var inner = new ConflictHandler(3);
        var handler = new OptimisticConcurrencyRetrierDecorator<CreateGreetingRequest, GreetingResponse>(inner);

        var action = async () => await handler.ExecuteAsync(new CreateGreetingRequest()).ConfigureAwait(false);

        await action.Should().ThrowAsync<OptimisticConcurrencyException>().ConfigureAwait(false);
        inner.Attempts.Should().Be(3);
    }

    /// <summary>Rejects a stale in-memory greeting version.</summary>
    [TestMethod]
    public async Task InMemoryStoreRejectsStaleVersion()
    {
        var store = new InMemoryGreetingStore();
        var greeting = new GreetingResponse { Id = Guid.NewGuid(), Message = "first" };

        await store.SaveAsync(greeting).ConfigureAwait(false);
        var stale = greeting with { Message = "stale", Version = greeting.Version!.ToArray() };
        var current = greeting with { Message = "current", Version = greeting.Version!.ToArray() };
        await store.SaveAsync(current).ConfigureAwait(false);

        var action = async () => await store.SaveAsync(stale).ConfigureAwait(false);

        await action.Should().ThrowAsync<OptimisticConcurrencyException>().ConfigureAwait(false);
    }

    private sealed class ConflictHandler : IRequestHandler<CreateGreetingRequest, GreetingResponse>
    {
        private readonly int _conflicts;

        public ConflictHandler(int conflicts)
        {
            _conflicts = conflicts;
        }

        public int Attempts { get; private set; }

        public Task<GreetingResponse> ExecuteAsync(CreateGreetingRequest request, CancellationToken ctk = default)
        {
            Attempts++;
            if (Attempts <= _conflicts)
                throw new OptimisticConcurrencyException("conflict");

            return Task.FromResult(new GreetingResponse { Id = Guid.Empty, Message = "ok" });
        }
    }
}
