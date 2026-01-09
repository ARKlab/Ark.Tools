using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Enum;

using System;

namespace Ark.Reference.Core.Common.Dto;

/// <summary>
/// Data transfer objects for Book entities
/// </summary>
public static class Book
{
    /// <summary>
    /// Version 1 of Book DTOs
    /// </summary>
    public static class V1
    {
        /// <summary>
        /// Data for creating a new Book entity
        /// </summary>
        public record Create
        {
            /// <summary>
            /// Gets or initializes the title of the Book
            /// </summary>
            public string? Title { get; init; }

            /// <summary>
            /// Gets or initializes the author of the Book
            /// </summary>
            public string? Author { get; init; }

            /// <summary>
            /// Gets or initializes the genre of the Book
            /// </summary>
            public BookGenre? Genre { get; init; }

            /// <summary>
            /// Gets or initializes the ISBN of the Book
            /// </summary>
            public string? ISBN { get; init; }
        }

        /// <summary>
        /// Data for updating a Book entity
        /// </summary>
        public record Update : Create
        {
            /// <summary>
            /// Gets or initializes the ID of the Book
            /// </summary>
            public int Id { get; init; }
        }

        /// <summary>
        /// Output representation of a Book entity
        /// </summary>
        public record Output : Update, IAuditEntity
        {
            /// <summary>
            /// Gets or initializes the description of the Book
            /// </summary>
            public string? Description { get; init; }

            /// <summary>
            /// Gets or initializes the audit ID associated with this entity
            /// </summary>
            public Guid AuditId { get; init; }
        }
    }
}