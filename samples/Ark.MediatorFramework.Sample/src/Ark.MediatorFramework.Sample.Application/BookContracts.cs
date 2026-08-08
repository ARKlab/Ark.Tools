// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Defines the supported categories for a book.</summary>
public enum BookGenre
{
    /// <summary>No category has been selected.</summary>
    NotSet,

    /// <summary>A fiction book.</summary>
    Fiction,

    /// <summary>A non-fiction book.</summary>
    NonFiction,

    /// <summary>A science book.</summary>
    Science,

    /// <summary>A technology book.</summary>
    Technology,
}

/// <summary>Represents a book managed by the sample application.</summary>
public sealed record BookResponse
{
    /// <summary>Gets the book identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the book title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the book author.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Gets the book category.</summary>
    public BookGenre Genre { get; init; }

    /// <summary>Gets the optional ISBN.</summary>
    public string? ISBN { get; init; }

    /// <summary>Gets the generated book description.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>Creates a book.</summary>
public sealed record CreateBookRequest : IRequest<CreateBookRequest, BookResponse>
{
    /// <summary>Gets the book title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the book author.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Gets the book category.</summary>
    public BookGenre Genre { get; init; }

    /// <summary>Gets the optional ISBN.</summary>
    public string? ISBN { get; init; }
}

/// <summary>Updates a book.</summary>
public sealed record UpdateBookRequest : IRequest<UpdateBookRequest, BookResponse>
{
    /// <summary>Gets the book identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the replacement title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the replacement author.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Gets the replacement category.</summary>
    public BookGenre Genre { get; init; }

    /// <summary>Gets the replacement ISBN.</summary>
    public string? ISBN { get; init; }
}

/// <summary>Deletes a book.</summary>
public sealed record DeleteBookRequest : IRequest<DeleteBookRequest, bool>
{
    /// <summary>Gets the book identifier.</summary>
    public Guid Id { get; init; }
}

/// <summary>Reads a book by identifier.</summary>
public sealed record GetBookQuery : IQuery<GetBookQuery, BookResponse>
{
    /// <summary>Gets the book identifier.</summary>
    public Guid Id { get; init; }
}

/// <summary>Searches books by their business fields.</summary>
public sealed record SearchBooksQuery : IQuery<SearchBooksQuery, BookPage>
{
    /// <summary>Gets the optional exact title filter.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the optional exact author filter.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the optional category filter.</summary>
    public BookGenre? Genre { get; init; }

    /// <summary>Gets the number of rows to skip.</summary>
    public int Skip { get; init; }

    /// <summary>Gets the maximum number of rows to return.</summary>
    public int Limit { get; init; } = 25;
}

/// <summary>Represents a page of books.</summary>
public sealed record BookPage
{
    /// <summary>Gets the total number of matching books.</summary>
    public long Count { get; init; }

    /// <summary>Gets the requested offset.</summary>
    public int Skip { get; init; }

    /// <summary>Gets the requested page size.</summary>
    public int Limit { get; init; }

    /// <summary>Gets the matching books.</summary>
    public IReadOnlyList<BookResponse> Data { get; init; } = [];
}
