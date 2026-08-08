// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Creates books through the application contract.</summary>
public sealed class CreateBookHandler : IRequestHandler<CreateBookRequest, BookResponse>
{
    private readonly IBookStore _store;

    /// <summary>Initializes a new instance of the <see cref="CreateBookHandler"/> class.</summary>
    public CreateBookHandler(IBookStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<BookResponse> ExecuteAsync(CreateBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = CreateResponse(Guid.NewGuid(), request.Title, request.Author, request.Genre, request.ISBN);
        return await _store.CreateAsync(book, ctk).ConfigureAwait(false);
    }

    internal static BookResponse CreateResponse(Guid id, string title, string author, BookGenre genre, string? isbn)
    {
        return new BookResponse
        {
            Id = id,
            Title = title,
            Author = author,
            Genre = genre,
            ISBN = isbn,
            Description = $"Book created: {title} by {author}",
        };
    }
}

/// <summary>Updates books through the application contract.</summary>
public sealed class UpdateBookHandler : IRequestHandler<UpdateBookRequest, BookResponse>
{
    private readonly IBookStore _store;

    /// <summary>Initializes a new instance of the <see cref="UpdateBookHandler"/> class.</summary>
    public UpdateBookHandler(IBookStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<BookResponse> ExecuteAsync(UpdateBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var book = CreateBookHandler.CreateResponse(request.Id, request.Title, request.Author, request.Genre, request.ISBN) with
        {
            Description = $"Book updated: {request.Title} by {request.Author}",
        };
        return await _store.UpdateAsync(book, ctk).ConfigureAwait(false);
    }
}

/// <summary>Deletes books through the application contract.</summary>
public sealed class DeleteBookHandler : IRequestHandler<DeleteBookRequest, bool>
{
    private readonly IBookStore _store;

    /// <summary>Initializes a new instance of the <see cref="DeleteBookHandler"/> class.</summary>
    public DeleteBookHandler(IBookStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(DeleteBookRequest request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _store.DeleteAsync(request.Id, ctk).ConfigureAwait(false);
        return true;
    }
}

/// <summary>Reads books through the application contract.</summary>
public sealed class GetBookHandler : IQueryHandler<GetBookQuery, BookResponse>
{
    private readonly IBookStore _store;

    /// <summary>Initializes a new instance of the <see cref="GetBookHandler"/> class.</summary>
    public GetBookHandler(IBookStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<BookResponse> ExecuteAsync(GetBookQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _store.GetAsync(query.Id, ctk).ConfigureAwait(false);
    }
}

/// <summary>Searches books through the application contract.</summary>
public sealed class SearchBooksHandler : IQueryHandler<SearchBooksQuery, BookPage>
{
    private readonly IBookStore _store;

    /// <summary>Initializes a new instance of the <see cref="SearchBooksHandler"/> class.</summary>
    public SearchBooksHandler(IBookStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<BookPage> ExecuteAsync(SearchBooksQuery query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _store.SearchAsync(query, ctk).ConfigureAwait(false);
    }
}
