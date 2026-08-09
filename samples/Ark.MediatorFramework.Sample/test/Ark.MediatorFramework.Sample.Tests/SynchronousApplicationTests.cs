// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using FluentValidation;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies synchronous application contracts without a transport host.</summary>
[TestClass]
public sealed class SynchronousApplicationTests
{
    /// <summary>Rejects every invalid request covered by the application validators.</summary>
    [TestMethod]
    public async Task ValidatorBackedContractsReportFieldFailures()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);

        var createBook = () => context.DispatchRequestAsync<Book_CreateRequest.V1, Book.V1.Output>(
            new Book_CreateRequest.V1(new Book.V1.Create()));
        (await createBook.Should().ThrowAsync<ValidationException>().ConfigureAwait(false))
            .Which.Errors.Should().Contain(error => error.PropertyName == "Data.Title");
        var updateBook = () => context.DispatchRequestAsync<Book_UpdateRequest.V1, Book.V1.Output>(
            new Book_UpdateRequest.V1(new Book.V1.Input(), Guid.Empty));
        (await updateBook.Should().ThrowAsync<ValidationException>().ConfigureAwait(false))
            .Which.Errors.Should().Contain(error => error.PropertyName == nameof(Book_UpdateRequest.V1.Id));
        var searchBooks = () => context.DispatchQueryAsync<Book_SearchQuery.V1, BookPage>(
            new Book_SearchQuery.V1 { Limit = 0 });
        (await searchBooks.Should().ThrowAsync<ValidationException>().ConfigureAwait(false))
            .Which.Errors.Should().Contain(error => error.PropertyName == nameof(Book_SearchQuery.V1.Limit));

        var createPrintProcess = () => context.DispatchRequestAsync<CreateBookPrintProcessRequest, BookPrintProcessResponse>(
            new CreateBookPrintProcessRequest());
        (await createPrintProcess.Should().ThrowAsync<ValidationException>().ConfigureAwait(false))
            .Which.Errors.Should().Contain(error => error.PropertyName == nameof(CreateBookPrintProcessRequest.BookId));
    }

    /// <summary>Observes cancellation from the application stream producer.</summary>
    [TestMethod]
    public async Task GreetingStreamObservesCancellation()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        using var cancellation = new CancellationTokenSource();
        var stream = await context.DispatchQueryAsync<GetGreetingsStreamQuery, IAsyncEnumerable<GreetingStreamItem>>(
            new GetGreetingsStreamQuery
            {
                Count = 10,
                DelayMilliseconds = 1,
            },
            cancellation.Token).ConfigureAwait(false);

        var items = new List<GreetingStreamItem>();
        await using var enumerator = stream.GetAsyncEnumerator(cancellation.Token);
        (await enumerator.MoveNextAsync().ConfigureAwait(false)).Should().BeTrue();
        items.Add(enumerator.Current);
        await cancellation.CancelAsync().ConfigureAwait(false);

        var action = async () => await enumerator.MoveNextAsync().ConfigureAwait(false);
        await action.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(false);
        items.Should().HaveCount(1);
    }
}
