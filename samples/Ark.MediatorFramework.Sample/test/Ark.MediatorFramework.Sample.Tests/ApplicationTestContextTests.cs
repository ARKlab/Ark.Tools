// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Fakes;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Outbox;

using AwesomeAssertions;

using FluentValidation;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies direct application composition and scope ownership.</summary>
[TestClass]
public sealed class ApplicationTestContextTests
{
    /// <summary>Dispatches a request through validation and audit decorators.</summary>
    [TestMethod]
    public async Task DirectDispatchRunsApplicationDecorators()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);

        var response = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create
            {
                Name = "Ada",
            })).ConfigureAwait(false);

        response.Message.Should().Contain("Hello, Ada!");
        context.AuditCount.Should().Be(1);
    }

    /// <summary>Reports validation failures from the decorated handler pipeline.</summary>
    [TestMethod]
    public async Task InvalidRequestThrowsValidationException()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        var action = () => context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create()));

        var exception = await action.Should().ThrowAsync<ValidationException>().ConfigureAwait(false);
        exception.Which.Errors.Should().Contain(error => error.PropertyName == "Data.Name");
    }

    /// <summary>Uses a new scoped graph for each top-level dispatch.</summary>
    [TestMethod]
    public async Task SequentialDispatchesUseDistinctScopes()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);

        var first = await context.DispatchRequestAsync<ScopeProbeRequest, Guid>(
            new ScopeProbeRequest()).ConfigureAwait(false);
        var second = await context.DispatchRequestAsync<ScopeProbeRequest, Guid>(
            new ScopeProbeRequest()).ConfigureAwait(false);

        first.Should().NotBe(second);
    }

    /// <summary>Reuses the current scope when a handler dispatches another contract.</summary>
    [TestMethod]
    public async Task NestedDispatchUsesCurrentScope()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);

        var observation = await context.DispatchRequestAsync<NestedScopeRequest, ScopeObservation>(
            new NestedScopeRequest()).ConfigureAwait(false);

        observation.OuterScopeId.Should().Be(observation.NestedScopeId);
    }

    /// <summary>Disposes scoped resources after a failed contract dispatch.</summary>
    [TestMethod]
    public async Task FailedDispatchDisposesItsScope()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        var action = () => context.DispatchRequestAsync<FailingScopeRequest, bool>(
            new FailingScopeRequest());

        await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
        context.FailedDispatchResourceDisposed.Should().BeTrue();
    }

    /// <summary>Rejects external calls after the scenario binding is detached.</summary>
    [TestMethod]
    public async Task ExternalServiceProxyFailsOutsideScenario()
    {
        var context = new ApplicationTestContext(useSqlStore: false);
        var proxy = context.PrintCompletedNotificationService;
        await context.DisposeAsync().ConfigureAwait(false);

        var action = () => proxy.NotifyAsync(new BookPrintProcessResponse());
        await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
    }

    /// <summary>Retries deterministic optimistic-concurrency failures before updating a greeting.</summary>
    [TestMethod]
    public async Task OptimisticConcurrencyDecoratorRetriesTransientFailures()
    {
        var faults = new ConcurrencyFaultInjector { PendingFailures = 2 };
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var decoratedFactory = new FaultInjectingSampleDataContextFactory(factory, faults);
        await using var context = new ApplicationTestContext(
            useSqlStore: false,
            dataContextFactory: decoratedFactory);

        var greeting = await context.DispatchRequestAsync<Greeting_CreateRequest.V1, Greeting.V1.Output>(
            new Greeting_CreateRequest.V1(new Greeting.V1.Create { Name = "Retry me" })).ConfigureAwait(false);
        var updated = await context.DispatchRequestAsync<Greeting_UpdateRequest.V1, Greeting.V1.Output>(
            new Greeting_UpdateRequest.V1(
                new Greeting.V1.Input { Message = "Retried successfully" },
                greeting.Id,
                greeting.ETag)).ConfigureAwait(false);

        updated.Message.Should().Be("Retried successfully");
        faults.PendingFailures.Should().Be(0);
    }

}
