// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Fakes;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Ark.Tools.Core.BusinessRuleViolation;

using FluentValidation;

using Rebus.Bus;

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

    /// <summary>Retries deterministic optimistic-concurrency failures before updating a greeting.</summary>
    [TestMethod]
    public async Task OptimisticConcurrencyDecoratorRetriesTransientFailures()
    {
        var audits = new InMemoryAuditStore();
        var faults = new ConcurrencyFaultInjector { PendingFailures = 2 };
        var store = new InMemoryGreetingStore(audits);
        var decoratedStore = new FaultInjectingGreetingStoreDecorator(store, faults);
        await using var context = new ApplicationTestContext(
            useSqlStore: false,
            greetingStore: decoratedStore,
            auditStore: audits);

        var greeting = await context.DispatchRequestAsync<CreateGreetingRequest, GreetingResponse>(
            new CreateGreetingRequest { Name = "Retry me" }).ConfigureAwait(false);
        var updated = await context.DispatchRequestAsync<UpdateGreetingMessageRequest, GreetingResponse>(
            new UpdateGreetingMessageRequest
            {
                Id = greeting.Id,
                Message = "Retried successfully",
                ETag = greeting.ETag,
            }).ConfigureAwait(false);

        updated.Message.Should().Be("Retried successfully");
        faults.PendingFailures.Should().Be(0);
    }

    /// <summary>Allows only one active print process when requests overlap.</summary>
    [TestMethod]
    public async Task ConcurrentPrintRequestsCreateOneActiveProcess()
    {
        var audits = new InMemoryAuditStore();
        var store = new CoordinatedBookStore(new InMemoryBookStore(audits));
        await using var context = new ApplicationTestContext(useSqlStore: false, bookStore: store, auditStore: audits);
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
        await using var context = new ApplicationTestContext();
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

    private sealed class CoordinatedBookStore : IBookStore
    {
        private readonly IBookStore _inner;
        private readonly TaskCompletionSource _bothRequestsArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        internal CoordinatedBookStore(IBookStore inner)
        {
            _inner = inner;
        }

        public async Task<BookResponse> CreateAsync(
            BookResponse book,
            AuditEntry? audit = null,
            CancellationToken ctk = default)
        {
            return await _inner.CreateAsync(book, audit, ctk).ConfigureAwait(false);
        }

        public async Task<BookResponse> GetAsync(Guid id, CancellationToken ctk = default)
        {
            return await _inner.GetAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<BookResponse> UpdateAsync(
            BookResponse book,
            AuditEntry? audit = null,
            CancellationToken ctk = default)
        {
            return await _inner.UpdateAsync(book, audit, ctk).ConfigureAwait(false);
        }

        public async Task DeleteAsync(Guid id, AuditEntry? audit = null, CancellationToken ctk = default)
        {
            await _inner.DeleteAsync(id, audit, ctk).ConfigureAwait(false);
        }

        public async Task<BookPage> SearchAsync(SearchBooksQuery query, CancellationToken ctk = default)
        {
            return await _inner.SearchAsync(query, ctk).ConfigureAwait(false);
        }

        public async Task<bool> TryCreateAndQueuePrintProcessAsync(
            BookPrintProcessResponse process,
            AuditEntry audit,
            IBus bus,
            CancellationToken ctk = default)
        {
            if (Interlocked.Increment(ref _requestCount) == 2)
                _bothRequestsArrived.TrySetResult();
            await _bothRequestsArrived.Task.WaitAsync(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
            return await _inner.TryCreateAndQueuePrintProcessAsync(process, audit, bus, ctk).ConfigureAwait(false);
        }

        public async Task<bool> TryCreatePrintProcessAsync(
            BookPrintProcessResponse process,
            CancellationToken ctk = default)
        {
            return await _inner.TryCreatePrintProcessAsync(process, ctk).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse> GetPrintProcessAsync(
            Guid id,
            CancellationToken ctk = default)
        {
            return await _inner.GetPrintProcessAsync(id, ctk).ConfigureAwait(false);
        }

        public async Task<BookPrintProcessResponse> UpdatePrintProcessAsync(
            BookPrintProcessResponse process,
            AuditEntry audit,
            CancellationToken ctk = default)
        {
            return await _inner.UpdatePrintProcessAsync(process, audit, ctk).ConfigureAwait(false);
        }
    }
}
