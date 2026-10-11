// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.Core;
using Ark.Tools.MediatorFramework;
using Ark.Tools.Solid;

using NodaTime;


using Vogen;

[assembly: SuppressMessage("Design", "MA0097:A class that implements IComparable<T> or IComparable should override comparison operators", Justification = "Vogen value objects are sortable through IComparable only.", Scope = "namespaceanddescendants", Target = "~N:Ark.MediatorFramework.AotSample.Core.API")]

namespace Ark.MediatorFramework.AotSample.Core.API;

/// <summary>Identifies a book: a Vogen value object.</summary>
/// <remarks>Over a <see cref="Guid"/>: Vogen's JSON converter for a numeric primitive uses reflection-based serialization.</remarks>
[ValueObject<Guid>]
public readonly partial struct BookId;

/// <summary>The genre of a book; it travels as an <see cref="EvolvableEnum{TEnum}"/>.</summary>
public enum Genre
{
    /// <summary>Not set.</summary>
    NOT_SET = 0,

    /// <summary>Fiction.</summary>
    Fiction,

    /// <summary>Non-fiction.</summary>
    NonFiction,
}

/// <summary>A book. Its members cover the Ark.Tools types with JSON and Dapper support.</summary>
public sealed record Book
{
    /// <summary>Gets the identifier.</summary>
    public BookId Id { get; init; }

    /// <summary>Gets the title.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Gets the genre.</summary>
    public EvolvableEnum<Genre> Genre { get; init; }

    /// <summary>Gets the publication date.</summary>
    public LocalDate PublishedOn { get; init; }

    /// <summary>Gets the release instant, with the publisher's offset.</summary>
    public OffsetDateTime ReleasedAt { get; init; }

    /// <summary>Gets the time of day the print slot opens.</summary>
    public LocalTime PrintSlot { get; init; }

    /// <summary>Gets the creation instant.</summary>
    public Instant CreatedAt { get; init; }

    /// <summary>Gets when the processor printed the book, in UTC.</summary>
    public LocalDateTime? PrintedAt { get; init; }

    /// <summary>Gets the publisher contact: a sensitive value, redacted in logs.</summary>
    public EmailAddress Contact { get; init; }

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

        /// <summary>Gets the genre.</summary>
        public EvolvableEnum<Genre> Genre { get; init; }

        /// <summary>Gets the publication date.</summary>
        public LocalDate PublishedOn { get; init; }

        /// <summary>Gets the release instant, with the publisher's offset.</summary>
        public OffsetDateTime ReleasedAt { get; init; }

        /// <summary>Gets the time of day the print slot opens.</summary>
        public LocalTime PrintSlot { get; init; }

        /// <summary>Gets the publisher contact.</summary>
        public EmailAddress Contact { get; init; }
    }
}

/// <summary>Reads a book.</summary>
public static class Book_GetQuery
{
    /// <summary>Version one of the book query.</summary>
    /// <remarks>A <see langword="null"/> result answers 404.</remarks>
    [HttpEndpoint("GET", "/api/v{version}/books/{id}", AllowAnonymous = true)]
    public sealed record V1 : IQuery<V1, Book?>
    {
        /// <summary>Gets the book identifier.</summary>
        [HttpRoute]
        public BookId Id { get; init; }
    }
}

/// <summary>Asks the processor to print a book.</summary>
[Message(Name = "aot/book.print")]
public sealed record PrintBookCommand : ICommand<PrintBookCommand>
{
    /// <summary>Gets the book identifier.</summary>
    public BookId BookId { get; init; }
}
