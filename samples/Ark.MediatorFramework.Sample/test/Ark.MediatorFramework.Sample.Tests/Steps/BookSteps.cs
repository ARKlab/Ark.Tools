// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Drivers;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines reusable current-book verbs for table-driven application scenarios.</summary>
[Binding]
public sealed class BookSteps
{
    private readonly BookDriver _books;

    /// <summary>Initializes a new instance of the <see cref="BookSteps"/> class.</summary>
    /// <param name="books">The scenario-owned book driver.</param>
    public BookSteps(BookDriver books)
    {
        _books = books;
    }

    /// <summary>Creates and activates a book from one table row.</summary>
    /// <param name="table">The create request data.</param>
    [Given("I create a book with")]
    [When("I create a book with")]
    public async Task CreateBook(Table table)
    {
        await _books.CreateAsync(table.CreateInstance<Book.V1.Create>()).ConfigureAwait(false);
    }

    /// <summary>Creates books from a table and activates the last created book.</summary>
    /// <param name="table">The create request data.</param>
    [Given("I create books with")]
    public async Task CreateBooks(Table table)
    {
        foreach (var input in table.CreateSet<Book.V1.Create>())
            await _books.CreateAsync(input).ConfigureAwait(false);
    }

    /// <summary>Loads the active book through its query contract.</summary>
    [When("I retrieve the current book")]
    public async Task RetrieveCurrentBook()
    {
        await _books.RetrieveCurrentAsync().ConfigureAwait(false);
    }

    /// <summary>Updates the active book from a table-defined request.</summary>
    /// <param name="table">The replacement book data.</param>
    [When("I update the current book with")]
    public async Task UpdateCurrentBook(Table table)
    {
        await _books.UpdateCurrentAsync(table.CreateInstance<Book.V1.Input>()).ConfigureAwait(false);
    }

    /// <summary>Deletes the active book.</summary>
    [When("I delete the current book")]
    public async Task DeleteCurrentBook()
    {
        await _books.DeleteCurrentAsync().ConfigureAwait(false);
    }

    /// <summary>Searches books using the supplied table filters.</summary>
    /// <param name="table">The search query data.</param>
    [When("I search books by")]
    public async Task SearchBooks(Table table)
    {
        await _books.SearchAsync(table.CreateInstance<Book_SearchQuery.V1>()).ConfigureAwait(false);
    }

    /// <summary>Asserts that the active book matches the supplied table.</summary>
    /// <param name="table">The expected book data.</param>
    [Then("the current book is")]
    public void CurrentBookIs(Table table)
    {
        table.CompareToInstance(_books.Current);
    }

    /// <summary>Asserts the current book-search result count.</summary>
    /// <param name="count">The expected count.</param>
    [Then(@"the book search has (.*) results")]
    public void BookSearchHasResults(long count)
    {
        _books.SearchResults.Should().NotBeNull();
        _books.SearchResults!.Count.Should().Be(count);
    }

    /// <summary>Asserts the current book-search result set.</summary>
    /// <param name="table">The expected books.</param>
    [Then("the book search contains")]
    public void BookSearchContains(Table table)
    {
        _books.SearchResults.Should().NotBeNull();
        table.CompareToSet(_books.SearchResults!.Data);
    }

    /// <summary>Asserts that the active book's audit matches the supplied table.</summary>
    /// <param name="table">The expected audit data.</param>
    [Then("the current book audit is")]
    public async Task CurrentBookAuditIs(Table table)
    {
        var audits = await _books.ReadCurrentAuditsAsync().ConfigureAwait(false);
        table.CompareToInstance(audits.Data.Single());
    }
}
