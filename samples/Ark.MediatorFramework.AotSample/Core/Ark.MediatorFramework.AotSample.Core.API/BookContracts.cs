// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.AotSample.Core.API;

/// <summary>A book.</summary>
public sealed record Book
{
    /// <summary>Gets the identifier.</summary>
    public int Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the processor has printed the book.</summary>
    public bool Printed { get; init; }
}

/// <summary>Creates a book and asks the processor to print it.</summary>
public static class Book_CreateRequest
{
    /// <summary>Version one of the book creation request.</summary>
    [HttpEndpoint("POST", "/api/v{version}/books", AllowAnonymous = true)]
    public sealed record V1 : IRequest<V1, Book>
    {
        /// <summary>Gets the title.</summary>
        public string Title { get; init; } = string.Empty;
    }
}

/// <summary>Reads a book.</summary>
public static class Book_GetQuery
{
    /// <summary>Version one of the book query.</summary>
    /// <remarks>A <see langword="null"/> result answers 404.</remarks>
    [HttpEndpoint("GET", "/api/v{version}/books/{id}", AllowAnonymous = true)]
    public sealed record V1 : IQuery<V1, Book>
    {
        /// <summary>Gets the book identifier.</summary>
        [HttpRoute]
        public int Id { get; init; }
    }
}

/// <summary>Asks the processor to print a book.</summary>
[Message(Name = "aot/book.print")]
public sealed record PrintBookCommand : ICommand<PrintBookCommand>
{
    /// <summary>Gets the book identifier.</summary>
    public int BookId { get; init; }
}
