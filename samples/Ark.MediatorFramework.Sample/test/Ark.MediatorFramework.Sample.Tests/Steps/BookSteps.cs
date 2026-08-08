// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines reusable table-driven contract steps for books.</summary>
[Binding]
public sealed class BookSteps
{
    private readonly SampleTestContext _sampleContext;
    private readonly Dictionary<string, BookResponse> _booksByTitle = new(StringComparer.Ordinal);
    private BookPage? _bookPage;

    /// <summary>Initializes a new instance of the <see cref="BookSteps"/> class.</summary>
    /// <param name="sampleContext">The scenario's direct application context.</param>
    public BookSteps(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Gets the active book in the current scenario.</summary>
    public BookResponse? Current { get; private set; }

    /// <summary>Creates and activates a book from one table row.</summary>
    /// <param name="table">The create request data.</param>
    [Given("I create a book with")]
    [When("I create a book with")]
    public async Task CreateBook(Table table)
    {
        var request = table.CreateInstance<CreateBookRequest>();
        Current = await Context.DispatchRequestAsync<CreateBookRequest, BookResponse>(request).ConfigureAwait(false);
        _booksByTitle[Current.Title] = Current;
    }

    /// <summary>Creates books from a table and activates the last created book.</summary>
    /// <param name="table">The create request data.</param>
    [Given("I create books with")]
    [When("I create books with")]
    public async Task CreateBooks(Table table)
    {
        foreach (var request in table.CreateSet<CreateBookRequest>())
            await CreateBook(request).ConfigureAwait(false);
    }

    /// <summary>Loads and activates a book identified by its title.</summary>
    /// <param name="title">The previously created book title.</param>
    [When(@"I retrieve the book ""(.*)""")]
    public async Task RetrieveBook(string title)
    {
        var known = GetBook(title);
        Current = await Context.DispatchQueryAsync<GetBookQuery, BookResponse>(
            new GetBookQuery { Id = known.Id }).ConfigureAwait(false);
        _booksByTitle[Current.Title] = Current;
    }

    /// <summary>Updates a book identified by its current title.</summary>
    /// <param name="title">The title used to locate the active book.</param>
    /// <param name="table">The replacement book data.</param>
    [When(@"I update the book ""(.*)"" with")]
    public async Task UpdateBook(string title, Table table)
    {
        var known = GetBook(title);
        var update = table.CreateInstance<UpdateBookRequest>() with { Id = known.Id };
        Current = await Context.DispatchRequestAsync<UpdateBookRequest, BookResponse>(update).ConfigureAwait(false);
        _booksByTitle.Remove(title);
        _booksByTitle[Current.Title] = Current;
    }

    /// <summary>Deletes a book identified by its title.</summary>
    /// <param name="title">The title used to locate the book.</param>
    [When(@"I delete the book ""(.*)""")]
    public async Task DeleteBook(string title)
    {
        var known = GetBook(title);
        await Context.DispatchRequestAsync<DeleteBookRequest, bool>(
            new DeleteBookRequest { Id = known.Id }).ConfigureAwait(false);
        _booksByTitle.Remove(title);
        Current = null;
    }

    /// <summary>Searches books using the supplied table filters.</summary>
    /// <param name="table">The search query data.</param>
    [When("I search books by")]
    public async Task SearchBooks(Table table)
    {
        var query = table.CreateInstance<SearchBooksQuery>();
        _bookPage = await Context.DispatchQueryAsync<SearchBooksQuery, BookPage>(query).ConfigureAwait(false);
    }

    /// <summary>Asserts that the active book matches the supplied table.</summary>
    /// <param name="table">The expected book data.</param>
    [Then("the current book is")]
    public void CurrentBookIs(Table table)
    {
        Current.Should().NotBeNull();
        table.CompareToInstance(Current!);
    }

    /// <summary>Asserts the current book-search result count.</summary>
    /// <param name="count">The expected count.</param>
    [Then(@"the book search has (.*) results")]
    public void BookSearchHasResults(long count)
    {
        _bookPage.Should().NotBeNull();
        _bookPage!.Count.Should().Be(count);
    }

    /// <summary>Asserts the current book-search result set.</summary>
    /// <param name="table">The expected books.</param>
    [Then("the book search contains")]
    public void BookSearchContains(Table table)
    {
        _bookPage.Should().NotBeNull();
        table.CompareToSet(_bookPage!.Data);
    }

    private async Task CreateBook(CreateBookRequest request)
    {
        Current = await Context.DispatchRequestAsync<CreateBookRequest, BookResponse>(request).ConfigureAwait(false);
        _booksByTitle[Current.Title] = Current;
    }

    private BookResponse GetBook(string title)
    {
        return _booksByTitle.TryGetValue(title, out var book)
            ? book
            : throw new InvalidOperationException($"Book '{title}' is not active in this scenario.");
    }

    private ApplicationTestContext Context => _sampleContext.Application;
}
