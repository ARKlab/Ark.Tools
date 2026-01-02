using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Enum;

using System;

namespace Ark.Reference.Core.Common.Dto
{
    /// <summary>
    /// Data transfer objects for Ping entities
    /// </summary>
    public static class Ping
    {
        /// <summary>
        /// Version 1 of Ping DTOs
        /// </summary>
        public static class V1
        {
            /// <summary>
            /// Data for creating a new Ping entity
            /// </summary>
            public record Create
            {
                /// <summary>
                /// Gets or initializes the name of the Ping
                /// </summary>
                public string? Name { get; init; }

                /// <summary>
                /// Gets or initializes the type of the Ping
                /// </summary>
                public PingType? Type { get; init; }
            }

            /// <summary>
            /// Data for updating a Ping entity
            /// </summary>
            public record Update : Create
            {
                /// <summary>
                /// Gets or initializes the ID of the Ping
                /// </summary>
                public int Id { get; init; }
            }

            /// <summary>
            /// Output representation of a Ping entity
            /// </summary>
            public record Output : Update, IAuditEntity
            {
                /// <summary>
                /// Gets or initializes the code of the Ping
                /// </summary>
                public string? Code { get; init; }

                /// <summary>
                /// Gets or initializes the audit ID associated with this entity
                /// </summary>
                public Guid AuditId { get; init; }
            }
        }
    }
}