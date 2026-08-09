// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
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
    public BookPage? SearchResults { get; private set; }

    private Book.V1.Output? _current;

    /// <summary>Creates and activates a book.</summary>
    /// <param name="input">The book creation fields.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task CreateAsync(Book.V1.Create input, CancellationToken ctk = default)
    {
        _current = await Context.DispatchRequestAsync<Book_CreateRequest.V1, Book.V1.Output>(
            new Book_CreateRequest.V1(input), ctk).ConfigureAwait(false);
    }

    /// <summary>Retrieves the active book through its query contract.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task RetrieveCurrentAsync(CancellationToken ctk = default)
    {
        _current = await Context.DispatchQueryAsync<Book_GetQuery.V1, Book.V1.Output>(
            new Book_GetQuery.V1(Current.Id),
            ctk).ConfigureAwait(false);
    }

    /// <summary>Updates the active book.</summary>
    /// <param name="input">The replacement book details.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task UpdateCurrentAsync(Book.V1.Input input, CancellationToken ctk = default)
    {
        _current = await Context.DispatchRequestAsync<Book_UpdateRequest.V1, Book.V1.Output>(
            new Book_UpdateRequest.V1(input, Current.Id),
            ctk).ConfigureAwait(false);
    }

    /// <summary>Deletes the active book.</summary>
    /// <param name="ctk">The cancellation token.</param>
    public async Task DeleteCurrentAsync(CancellationToken ctk = default)
    {
        await Context.DispatchRequestAsync<Book_DeleteRequest.V1, bool>(
            new Book_DeleteRequest.V1(Current.Id),
            ctk).ConfigureAwait(false);
        _current = null;
    }

    /// <summary>Searches books through the query contract.</summary>
    /// <param name="query">The search query.</param>
    /// <param name="ctk">The cancellation token.</param>
    public async Task SearchAsync(Book_SearchQuery.V1 query, CancellationToken ctk = default)
    {
        SearchResults = await Context.DispatchQueryAsync<Book_SearchQuery.V1, BookPage>(query, ctk).ConfigureAwait(false);
    }

    /// <summary>Reads audit records for the active book.</summary>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The matching audit records.</returns>
    public async Task<PagedResult<AuditRecord>> ReadCurrentAuditsAsync(CancellationToken ctk = default)
    {
        return await Context.DispatchQueryAsync<GetAuditsQuery, PagedResult<AuditRecord>>(
            new GetAuditsQuery
            {
                Identifier = Current.Id.ToString("D"),
                Limit = 25,
            },
            ctk).ConfigureAwait(false);
    }

    private ApplicationTestContext Context => _sampleContext.Application;
}
