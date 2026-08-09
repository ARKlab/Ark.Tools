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
        var faults = new ConcurrencyFaultInjector { PendingFailures = 2 };
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var decoratedFactory = new FaultInjectingSampleDataContextFactory(factory, faults);
        await using var context = new ApplicationTestContext(
            useSqlStore: false,
            dataContextFactory: decoratedFactory);

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
        var factory = new InMemorySampleDataContextFactory(new InMemoryOutboxContextFactory());
        var coordinatedFactory = new CoordinatedSampleDataContextFactory(factory);
        await using var context = new ApplicationTestContext(
            useSqlStore: false,
            dataContextFactory: coordinatedFactory);
        context.SetAuthenticatedUser();
        context.StartOutboundBus();
        var book = await context.DispatchRequestAsync<Book_CreateRequest.V1, Book.V1.Output>(
            new Book_CreateRequest.V1(new Book.V1.Create
            {
                Title = "Concurrent Systems",
                Author = "Test Author",
                Genre = Book.V1.Genre.Technology,
            })).ConfigureAwait(false);
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
        var book = await context.DispatchRequestAsync<Book_CreateRequest.V1, Book.V1.Output>(
            new Book_CreateRequest.V1(new Book.V1.Create
            {
                Title = "Reliable Systems",
                Author = "Test Author",
                Genre = Book.V1.Genre.Technology,
            })).ConfigureAwait(false);
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

    private sealed class CoordinatedSampleDataContextFactory : ISampleDataContextFactory
    {
        private readonly ISampleDataContextFactory _inner;
        private readonly Coordinator _coordinator = new();

        internal CoordinatedSampleDataContextFactory(ISampleDataContextFactory inner)
        {
            _inner = inner;
        }

        public async Task<ISampleDataContext> CreateAsync(CancellationToken ctk = default)
        {
            var context = await _inner.CreateAsync(ctk).ConfigureAwait(false);
            return new CoordinatedContext(context, _coordinator);
        }

        async Task<Ark.Tools.Outbox.IOutboxAsyncContext> Ark.Tools.Outbox.IOutboxAsyncContextFactory.CreateAsync(
            CancellationToken ctk)
        {
            return await _inner.CreateAsync(ctk).ConfigureAwait(false);
        }

        private sealed class CoordinatedContext : ISampleDataContext
        {
            private readonly ISampleDataContext _inner;
            private readonly Coordinator _coordinator;

            public CoordinatedContext(
                ISampleDataContext inner,
                Coordinator coordinator)
            {
                _inner = inner;
                _coordinator = coordinator;
            }

            public Ark.Tools.Outbox.IOutboxContextCore OutboxContext => _inner.OutboxContext;

            public async Task SaveAsync(GreetingResponse greeting, CancellationToken ctk = default) =>
                await _inner.SaveAsync(greeting, ctk).ConfigureAwait(false);

            public async Task WriteAuditAsync(AuditEntry audit, CancellationToken ctk = default) =>
                await _inner.WriteAuditAsync(audit, ctk).ConfigureAwait(false);

            public async Task<GreetingResponse?> ReadAsync(Guid id, CancellationToken ctk = default) =>
                await _inner.ReadAsync(id, ctk).ConfigureAwait(false);

            public async Task<IReadOnlyCollection<GreetingResponse>> ReadAllAsync(CancellationToken ctk = default) =>
                await _inner.ReadAllAsync(ctk).ConfigureAwait(false);

            public async Task<GreetingResponse?> UpdateAsync(Guid id, string message, string eTag, Guid auditId, CancellationToken ctk = default) =>
                await _inner.UpdateAsync(id, message, eTag, auditId, ctk).ConfigureAwait(false);

            public async Task<PagedResult<AuditRecord>> ReadAuditsAsync(GetAuditsQuery query, CancellationToken ctk = default) =>
                await _inner.ReadAuditsAsync(query, ctk).ConfigureAwait(false);

            public async Task<GreetingPage> ReadGreetingsAsync(SearchGreetingsQuery query, CancellationToken ctk = default) =>
                await _inner.ReadGreetingsAsync(query, ctk).ConfigureAwait(false);

            public async Task CommitAsync(CancellationToken ctk = default) =>
                await _inner.CommitAsync(ctk).ConfigureAwait(false);

            public async Task SaveBookAsync(Book.V1.Output book, CancellationToken ctk = default) =>
                await _inner.SaveBookAsync(book, ctk).ConfigureAwait(false);

            public async Task<Book.V1.Output?> ReadBookAsync(Guid id, CancellationToken ctk = default) =>
                await _inner.ReadBookAsync(id, ctk).ConfigureAwait(false);

            public async Task<bool> UpdateBookAsync(Book.V1.Output book, CancellationToken ctk = default) =>
                await _inner.UpdateBookAsync(book, ctk).ConfigureAwait(false);

            public async Task<bool> DeleteBookAsync(Guid id, CancellationToken ctk = default) =>
                await _inner.DeleteBookAsync(id, ctk).ConfigureAwait(false);

            public async Task<Book.V1.Page> ReadBooksAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default) =>
                await _inner.ReadBooksAsync(query, ctk).ConfigureAwait(false);

            public async Task<bool> TrySaveBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
            {
                if (Interlocked.Increment(ref _coordinator.RequestCount) == 2)
                    _coordinator.BothRequestsArrived.TrySetResult();
                await _coordinator.BothRequestsArrived.Task.WaitAsync(TimeSpan.FromSeconds(5), ctk).ConfigureAwait(false);
                return await _inner.TrySaveBookPrintProcessAsync(process, ctk).ConfigureAwait(false);
            }

            public async Task<BookPrintProcessResponse?> ReadBookPrintProcessAsync(Guid id, CancellationToken ctk = default) =>
                await _inner.ReadBookPrintProcessAsync(id, ctk).ConfigureAwait(false);

            public async Task<bool> UpdateBookPrintProcessAsync(BookPrintProcessResponse process, CancellationToken ctk = default) =>
                await _inner.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false);

            public async Task CommitAsync(bool reuse, CancellationToken ctk = default) =>
                await _inner.CommitAsync(reuse, ctk).ConfigureAwait(false);

            public async ValueTask DisposeAsync() =>
                await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private sealed class Coordinator
        {
            internal readonly TaskCompletionSource BothRequestsArrived =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal int RequestCount;
        }
    }
}
