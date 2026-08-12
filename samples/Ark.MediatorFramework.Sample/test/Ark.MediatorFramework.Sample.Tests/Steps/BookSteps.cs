// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Drivers;

using Ark.Tools.Reqnroll;
using Ark.Tools.Authorization;

using AwesomeAssertions;

using Reqnroll;
using Reqnroll.Assist;

using System.Text;

namespace Ark.MediatorFramework.Sample.Tests.Steps;

/// <summary>Defines table data for a book cover attachment.</summary>
public sealed record BookCoverTable
{
    /// <summary>Gets the attachment file name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the attachment MIME content type.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Gets the UTF-8 attachment content.</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>Defines reusable current-book verbs for table-driven application scenarios.</summary>
[Binding]
public sealed class BookSteps
{
    private readonly BookDriver _books;
    private Exception? _exception;

    /// <summary>Initializes a new instance of the <see cref="BookSteps"/> class.</summary>
    /// <param name="books">The scenario-owned book driver.</param>
    public BookSteps(BookDriver books)
    {
        _books = books;
    }

    /// <summary>Creates and activates a book from one table row.</summary>
    /// <param name="table">The create request data.</param>
    [Given("I create a book with")]
    public async Task GivenCreateBook(Table table)
    {
        await CreateBook(table).ConfigureAwait(false);
        _books.Current.Should().NotBeNull();
    }

    [When("I create a book with")]
    public async Task CreateBook(Table table)
    {
        await _books.CreateAsync(table.CreateInstance<Book.V1.Create>()).ConfigureAwait(false);
    }

    /// <summary>Creates books from a table and activates the last created book.</summary>
    /// <param name="table">The create request data.</param>
    [Given("I create books with")]
    public async Task GivenCreateBooks(Table table)
    {
        await CreateBooks(table).ConfigureAwait(false);
        _books.Current.Should().NotBeNull();
    }

    [When("I create books with")]
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
        var merged = table.MergeInstance(_books.Current);
        await _books.UpdateCurrentAsync(new Book.V1.Input
        {
            Title = merged.Title,
            Author = merged.Author,
            Genre = merged.Genre,
        }).ConfigureAwait(false);
    }

    /// <summary>Deletes the active book.</summary>
    [When("I delete the current book")]
    public async Task DeleteCurrentBook()
    {
        await _books.DeleteCurrentAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts that the active book was deleted successfully.</summary>
    [Then("the current book was deleted")]
    public void CurrentBookWasDeleted()
    {
        _books.HasCurrent.Should().BeFalse();
    }

    /// <summary>Searches books using the supplied table filters.</summary>
    /// <param name="table">The search query data.</param>
    [When("I search books by")]
    public async Task SearchBooks(Table table)
    {
        await _books.SearchAsync(table.CreateInstance<Book_SearchQuery.V1>()).ConfigureAwait(false);
    }

    /// <summary>Uploads a cover for the active book.</summary>
    /// <param name="table">The cover attachment data.</param>
    [When("I upload a cover for the current book with")]
    public async Task UploadBookCover(Table table)
    {
        var cover = table.CreateInstance<BookCoverTable>();
        _exception = await _captureAsync(() => _books.UploadCoverAsync(_createAttachment(cover)))
            .ConfigureAwait(false);
    }

    /// <summary>Downloads the cover for the active book.</summary>
    [When("I download the cover for the current book")]
    public async Task DownloadBookCover()
    {
        _exception = await _captureAsync(_books.DownloadCoverAsync).ConfigureAwait(false);
    }

    /// <summary>Asserts the metadata and byte count reported by a cover upload.</summary>
    /// <param name="table">The expected upload response.</param>
    [Then("the book cover upload is")]
    public void BookCoverUploadIs(Table table)
    {
        _exception.Should().BeNull();
        table.CompareToInstance(_books.CoverUpload);
    }

    /// <summary>Asserts the metadata and UTF-8 content of the downloaded cover.</summary>
    /// <param name="table">The expected cover data.</param>
    [Then("the current book cover is")]
    public async Task CurrentBookCoverIs(Table table)
    {
        _exception.Should().BeNull();
        _books.Cover.Should().NotBeNull();
        var stream = _books.Cover!.OpenRead();
        await using var __ctx = stream.ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
        var cover = new BookCoverTable
        {
            Name = _books.Cover.Name,
            ContentType = _books.Cover.ContentType,
            Content = await reader.ReadToEndAsync().ConfigureAwait(false),
        };
        table.CompareToInstance(cover);
    }

    /// <summary>Asserts that a cover operation failed because authorization was denied.</summary>
    [Then("the book cover request fails with an authorization exception")]
    public void BookCoverRequestFailsWithAuthorizationException()
    {
        _exception.Should().BeOfType<PolicyAuthorizationException>();
    }

    /// <summary>Asserts that a cover operation failed because the cover was missing.</summary>
    [Then("the book cover request fails because the cover is missing")]
    public void BookCoverRequestFailsBecauseCoverIsMissing()
    {
        _exception.Should().BeOfType<EntityNotFoundException>();
    }

    /// <summary>Asserts that an invalid cover was rejected by validation.</summary>
    [Then("the book cover request fails validation")]
    public void BookCoverRequestFailsValidation()
    {
        _exception.Should().BeOfType<FluentValidation.ValidationException>();
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

    private static ArkAttachment _createAttachment(BookCoverTable cover)
    {
        var content = Encoding.UTF8.GetBytes(cover.Content);
        return new ArkAttachment(cover.Name, cover.ContentType, () => new MemoryStream(content, writable: false));
    }

    private static async Task<Exception?> _captureAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
#pragma warning disable ERP022 // Reqnroll needs the exception for the later assertion.
            return exception;
#pragma warning restore ERP022
        }
    }
}
