// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using Ark.Tools.Core;

namespace Ark.MediatorFramework.Sample.Tests.Drivers;

/// <summary>Owns the active book and book-contract dispatch for one Reqnroll scenario.</summary>
public sealed class BookDriver
{
    private readonly SampleTestContext _sampleContext;

    /// <summary>Initializes a new instance of the <see cref="BookDriver"/> class.</summary>
    /// <param name="sampleContext">The scenario-owned application context.</param>
    public BookDriver(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Gets the active book for the scenario.</summary>
    public Book.V1.Output Current => _current ?? throw new InvalidOperationException("No current book is available in this scenario.");

    /// <summary>Gets the latest book search page.</summary>
    public Book.V1.Page? SearchResults { get; private set; }

    /// <summary>Gets the latest cover upload response.</summary>
    public UploadResponse? CoverUpload { get; private set; }

    /// <summary>Gets the downloaded cover.</summary>
    public IArkAttachment? Cover { get; private set; }

    /// <summary>Gets the latest created review.</summary>
    public BookReview? CurrentReview { get; private set; }

    /// <summary>Gets the latest review list.</summary>
    public IReadOnlyList<BookReview>? Reviews { get; private set; }

    /// <summary>Gets the latest recorded reading activity.</summary>
    public ReadingActivity? CurrentActivity { get; private set; }

    /// <summary>Gets the latest reading activity list.</summary>
    public IReadOnlyList<ReadingActivity>? Activities { get; private set; }

    /// <summary>Gets whether the scenario has an active book.</summary>
    public bool HasCurrent => _current is not null;

    private Book.V1.Output? _current;

    /// <summary>Creates and activates a book.</summary>
    /// <param name="input">The book creation fields.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task CreateAsync(Book.V1.Create input, CancellationToken ctk = default)
    {
        _current = await _context.DispatchRequestAsync<Book_CreateRequest.V1, Book.V1.Output>(
            new Book_CreateRequest.V1(input), ctk).ConfigureAwait(false);
    }

    /// <summary>Retrieves the active book through its query contract.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task RetrieveCurrentAsync(CancellationToken ctk = default)
    {
        _current = await _context.DispatchQueryAsync<Book_GetQuery.V1, Book.V1.Output>(
            new Book_GetQuery.V1(Current.Id),
            ctk).ConfigureAwait(false);
    }

    /// <summary>Updates the active book.</summary>
    /// <param name="input">The replacement book details.</param>
    /// <param name="eTag">The optional ETag precondition.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task UpdateCurrentAsync(Book.V1.Input input, string? eTag = null, CancellationToken ctk = default)
    {
        _current = await _context.DispatchRequestAsync<Book_UpdateRequest.V1, Book.V1.Output>(
            new Book_UpdateRequest.V1(input, Current.Id, eTag ?? Current.ETag),
            ctk).ConfigureAwait(false);
    }

    /// <summary>Deletes the active book.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task DeleteCurrentAsync(CancellationToken ctk = default)
    {
        await _context.DispatchRequestAsync<Book_DeleteRequest.V1, bool>(
            new Book_DeleteRequest.V1(Current.Id),
            ctk).ConfigureAwait(false);
        _current = null;
    }

    /// <summary>Searches books through the query contract.</summary>
    /// <param name="query">The search query.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task SearchAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default)
    {
        SearchResults = await _context.DispatchQueryAsync<Book_SearchQuery.V1, Book.V1.Page>(query, ctk).ConfigureAwait(false);
    }

    /// <summary>Uploads a cover for the active book.</summary>
    /// <param name="attachment">The cover attachment.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task UploadCoverAsync(IArkAttachment attachment, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        CoverUpload = await _context.DispatchRequestAsync<UploadBookCoverRequest, UploadResponse>(
            new UploadBookCoverRequest
            {
                Id = Current.Id,
                Attachment = attachment,
            },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Downloads the cover for the active book.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task DownloadCoverAsync(CancellationToken ctk = default)
    {
        Cover = await _context.DispatchQueryAsync<DownloadBookCoverQuery, IArkAttachment>(
            new DownloadBookCoverQuery { Id = Current.Id },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Creates a review for the active book.</summary>
    public async Task CreateReviewAsync(int rating, string text, CancellationToken ctk = default)
    {
        CurrentReview = await _context.DispatchRequestAsync<CreateBookReviewRequest, BookReview>(
            new CreateBookReviewRequest
            {
                BookId = Current.Id,
                Rating = rating,
                Text = text,
            },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Lists reviews for the active book.</summary>
    public async Task ListReviewsAsync(int skip = 0, int limit = 25, CancellationToken ctk = default)
    {
        Reviews = await _context.DispatchQueryAsync<ListBookReviewsQuery, IReadOnlyList<BookReview>>(
            new ListBookReviewsQuery
            {
                BookId = Current.Id,
                Skip = skip,
                Limit = limit,
            },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Records reading activity for the active book.</summary>
    public async Task RecordActivityAsync(
        ReadingActivityKind kind,
        int progress,
        CancellationToken ctk = default)
    {
        CurrentActivity = await _context.DispatchRequestAsync<RecordReadingActivityRequest, ReadingActivity>(
            new RecordReadingActivityRequest
            {
                BookId = Current.Id,
                Kind = kind,
                Progress = progress,
            },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Reads recent activity for the active book.</summary>
    public async Task ReadActivitiesAsync(int limit = 25, CancellationToken ctk = default)
    {
        Activities = await _context.DispatchQueryAsync<GetReadingActivityQuery, IReadOnlyList<ReadingActivity>>(
            new GetReadingActivityQuery
            {
                BookId = Current.Id,
                Limit = limit,
            },
            ctk).ConfigureAwait(false);
    }

    /// <summary>Reads audit records for the active book.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The matching audit records.</returns>
    public async Task<PagedResult<AuditRecord>> ReadCurrentAuditsAsync(CancellationToken ctk = default)
    {
        return await _context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = Current.Id.ToString("D"),
                Limit = 25,
            },
            ctk).ConfigureAwait(false);
    }

    private ApplicationTestContext _context => _sampleContext.Application;
}
