// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Ark.Tools.Core.BusinessRuleViolation;

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

    /// <summary>Allows only one active print process when requests overlap.</summary>
    [TestMethod]
    public async Task ConcurrentPrintRequestsCreateOneActiveProcess()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser();
        context.StartOutboundBus();
        var book = await context.DispatchRequestAsync<CreateBookRequest, BookResponse>(
            new CreateBookRequest
            {
                Title = "Concurrent Systems",
                Author = "Test Author",
                Genre = BookGenre.Technology,
            }).ConfigureAwait(false);
        var request = new CreateBookPrintProcessRequest { BookId = book.Id };

        var attempts = await Task.WhenAll(
            CaptureAsync(() => context.DispatchRequestAsync<CreateBookPrintProcessRequest, BookPrintProcessResponse>(request)),
            CaptureAsync(() => context.DispatchRequestAsync<CreateBookPrintProcessRequest, BookPrintProcessResponse>(request)))
            .ConfigureAwait(false);

        attempts.Count(static exception => exception is null).Should().Be(1);
        var violation = attempts.Single(static exception => exception is not null)
            .Should().BeOfType<BusinessRuleViolationException>().Which;
        violation.BusinessRuleViolation.Should().BeOfType<BookPrintingProcessAlreadyRunningViolation>();
    }

    /// <summary>Resumes a print process that was interrupted after entering the running state.</summary>
    [TestMethod]
    public async Task RedeliveryResumesRunningPrintProcess()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser();
        context.StartOutboundBus();
        var book = await context.DispatchRequestAsync<CreateBookRequest, BookResponse>(
            new CreateBookRequest
            {
                Title = "Reliable Systems",
                Author = "Test Author",
                Genre = BookGenre.Technology,
            }).ConfigureAwait(false);
        var process = await context.DispatchRequestAsync<CreateBookPrintProcessRequest, BookPrintProcessResponse>(
            new CreateBookPrintProcessRequest { BookId = book.Id }).ConfigureAwait(false);
        process = await context.BookStore.UpdatePrintProcessAsync(
            process with
            {
                Progress = 0.5,
                Status = BookPrintProcessStatus.Running,
            },
            new AuditEntry
            {
                Id = Guid.NewGuid(),
                UserId = "application-test-user",
                EntityType = nameof(BookPrintProcessResponse),
                Identifier = process.Id.ToString("D"),
                Operation = nameof(ProcessBookPrintProcessRequest),
                Timestamp = context.Clock.GetCurrentInstant(),
            }).ConfigureAwait(false);

        var completed = await context.DispatchRequestAsync<ProcessBookPrintProcessRequest, BookPrintProcessResponse>(
            new ProcessBookPrintProcessRequest { Id = process.Id }).ConfigureAwait(false);

        completed.Status.Should().Be((Ark.Tools.Core.EvolvableEnum<BookPrintProcessStatus>)BookPrintProcessStatus.Completed);
        completed.Progress.Should().Be(1);
    }

    private static async Task<Exception?> CaptureAsync<T>(Func<Task<T>> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
#pragma warning disable ERP022 // The test asserts the exact exception after both requests finish.
            return exception;
#pragma warning restore ERP022
        }
    }
}
