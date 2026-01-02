using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries
{
    /// <summary>
    /// Query to retrieve a Ping entity by name (for testing/demonstration)
    /// </summary>
    public static class Ping_GetByNameQuery
    {
        /// <summary>
        /// Version 1 of the GetByName query
        /// </summary>
        public record V1 : IQuery<Ping.V1.Output>
        {
            /// <summary>
            /// Gets or initializes the name of the Ping to retrieve
            /// </summary>
            public string? Name { get; init; }
        }
    }
}