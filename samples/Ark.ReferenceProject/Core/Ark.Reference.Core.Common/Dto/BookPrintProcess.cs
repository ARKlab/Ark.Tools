using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Enum;

using System;

namespace Ark.Reference.Core.Common.Dto
{
    /// <summary>
    /// Data transfer objects for BookPrintProcess entities
    /// </summary>
    public static class BookPrintProcess
    {
        /// <summary>
        /// Version 1 of BookPrintProcess DTOs
        /// </summary>
        public static class V1
        {
            /// <summary>
            /// Data for creating a new BookPrintProcess entity
            /// </summary>
            public record Create
            {
                /// <summary>
                /// Gets or initializes the Book ID to print
                /// </summary>
                public int BookId { get; init; }

                /// <summary>
                /// Gets or initializes whether the process should fail (for testing)
                /// </summary>
                public bool ShouldFail { get; init; }
            }

            /// <summary>
            /// Output representation of a BookPrintProcess entity
            /// </summary>
            public record Output : IAuditEntity
            {
                /// <summary>
                /// Gets or initializes the ID of the BookPrintProcess
                /// </summary>
                public int BookPrintProcessId { get; init; }

                /// <summary>
                /// Gets or initializes the Book ID being printed
                /// </summary>
                public int BookId { get; init; }

                /// <summary>
                /// Gets or initializes the progress (0.0 to 1.0)
                /// </summary>
                public double Progress { get; init; }

                /// <summary>
                /// Gets or initializes the status of the print process
                /// </summary>
                public BookPrintProcessStatus Status { get; init; }

                /// <summary>
                /// Gets or initializes the error message if Status is Error
                /// </summary>
                public string? ErrorMessage { get; init; }

                /// <summary>
                /// Gets or initializes whether the process should fail (for testing)
                /// </summary>
                public bool ShouldFail { get; init; }

                /// <summary>
                /// Gets or initializes the audit ID associated with this entity
                /// </summary>
                public Guid AuditId { get; init; }
            }
        }
    }
}