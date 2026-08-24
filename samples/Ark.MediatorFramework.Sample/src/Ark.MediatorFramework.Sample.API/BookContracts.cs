// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

using Ark.MediatorFramework.Sample.API.Authorization;

namespace Ark.MediatorFramework.Sample.API;

/// <summary>Defines the versioned book model.</summary>
public static class Book
{
    /// <summary>Defines version one of the book model.</summary>
    public static class V1
    {
        /// <summary>Defines the supported categories for a book.</summary>
        public enum Genre
        {
            /// <summary>No category has been selected.</summary>
            NOT_SET = 0,

            /// <summary>A fiction book.</summary>
            Fiction,

            /// <summary>A non-fiction book.</summary>
            NonFiction,

            /// <summary>A science book.</summary>
            Science,

            /// <summary>A technology book.</summary>
            Technology,
        }

        /// <summary>Fields accepted when a book is written.</summary>
        public record Input
        {
            /// <summary>Gets the book title.</summary>
            public string Title { get; init; } = string.Empty;

            /// <summary>Gets the book author.</summary>
            public string Author { get; init; } = string.Empty;

            /// <summary>Gets the book category.</summary>
            public EvolvableEnum<Genre> Genre { get; init; }

        }

        /// <summary>Fields accepted when a book is created.</summary>
        public record Create : Input
        {
            /// <summary>Gets the optional ISBN assigned when the book is created.</summary>
            public string? ISBN { get; init; }
        }

        /// <summary>Fields accepted when a book is updated.</summary>
        public record Update : Input;

        /// <summary>Fields returned for a book.</summary>
        public record Output : Create
        {
            /// <summary>Gets the book identifier.</summary>
            [ServerSet]
            public Guid Id { get; init; }

            /// <summary>Gets the generated book description.</summary>
            [ServerSet]
            public string Description { get; init; } = string.Empty;

            /// <summary>Gets the opaque concurrency token.</summary>
            [ETag]
            public string? ETag { get; init; }
        }

        /// <summary>Represents a page of books.</summary>
        public sealed record Page
        {
            /// <summary>Gets the total number of matching books.</summary>
            public long Count { get; init; }

            /// <summary>Gets the requested offset.</summary>
            public int Skip { get; init; }

            /// <summary>Gets the requested page size.</summary>
            public int Limit { get; init; }

            /// <summary>Gets the matching books.</summary>
            public IReadOnlyList<Output> Data { get; init; } = [];
        }
    }
}

/// <summary>Creates a book.</summary>
public static class Book_CreateRequest
{
    /// <summary>Version one of the book creation request.</summary>
    [McpTool(
        Name = "books.create",
        ReadOnly = false,
        Destructive = false)]
    [RequireScopePolicy(ApplicationScopes.BookWrite)]
    public sealed record V1([property: HttpBody] Book.V1.Create Data) :
        IRequest<V1, Book.V1.Output>;
}

/// <summary>Creates multiple books.</summary>
public static class Book_BulkCreateRequest
{
    /// <summary>Version one of the bulk book creation request.</summary>
    [HttpEndpoint("POST", "/api/v{version}/books/bulk")]
    [RequireScopePolicy(ApplicationScopes.BookWrite)]
    public sealed record V1(
        [property: HttpBody] IReadOnlyList<Book.V1.Create> Data) :
        IRequest<V1, IReadOnlyList<Book.V1.Output>>;
}

/// <summary>Updates a book.</summary>
public static class Book_UpdateRequest
{
    /// <summary>Version one of the book update request.</summary>
    [RequireScopePolicy(ApplicationScopes.BookWrite)]
    public sealed record V1(
        [property: HttpBody] Book.V1.Input Data,
        [property: HttpRoute] Guid Id,
        [property: ETag] string? ETag = null) : IRequest<V1, Book.V1.Output>;
}

/// <summary>Deletes a book.</summary>
public static class Book_DeleteRequest
{
    /// <summary>Version one of the book deletion request.</summary>
    [RequireScopePolicy(ApplicationScopes.BookWrite)]
    public sealed record V1(Guid Id) : IRequest<V1, bool>;
}

/// <summary>Reads a book by identifier.</summary>
public static class Book_GetQuery
{
    /// <summary>Version one of the book query.</summary>
    [McpTool(Name = "books.get")]
    [RequireScopePolicy(ApplicationScopes.BookRead)]
    public sealed record V1(Guid Id) : IQuery<V1, Book.V1.Output>;
}

/// <summary>Searches books by their business fields.</summary>
public static class Book_SearchQuery
{
    /// <summary>Version one of the book search query.</summary>
    [RequireScopePolicy(ApplicationScopes.BookRead)]
    public sealed record V1 : IQuery<V1, Book.V1.Page>, IQueryPaged
    {
        /// <summary>Gets the optional exact title filter.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the optional exact author filter.</summary>
        public string? Author { get; init; }

        /// <summary>Gets the optional category filter.</summary>
        public EvolvableEnum<Book.V1.Genre>? Genre { get; init; }

        /// <summary>Gets the number of rows to skip.</summary>
        public int Skip { get; set; }

        /// <summary>Gets the maximum number of rows to return.</summary>
        public int Limit { get; init; } = 25;

        /// <summary>Gets the sort expressions applied to matching books.</summary>
        public IEnumerable<string> Sort { get; init; } = [];
    }
}
