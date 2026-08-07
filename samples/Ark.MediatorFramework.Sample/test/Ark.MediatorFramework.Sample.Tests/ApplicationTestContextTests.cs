// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

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

        var response = await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest
            {
                Name = "Ada",
            }).ConfigureAwait(false);

        response.Message.Should().Contain("Hello, Ada!");
        context.AuditCount.Should().Be(1);
    }

    /// <summary>Reports validation failures from the decorated handler pipeline.</summary>
    [TestMethod]
    public async Task InvalidRequestThrowsValidationException()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        var action = () => context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest());

        var exception = await action.Should().ThrowAsync<ValidationException>().ConfigureAwait(false);
        exception.Which.Errors.Should().Contain(error => error.PropertyName == nameof(CreateGreetingRequest.Name));
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
}
