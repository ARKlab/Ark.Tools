using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries;

/// <summary>
/// Query to retrieve a Ping entity by its ID
/// </summary>
public static class Ping_GetByIdQuery
{
    /// <summary>
    /// Version 1 of the GetById query
    /// </summary>
    public record V1 : IQuery<Ping.V1.Output?>
    {
        /// <summary>
        /// Gets or initializes the ID of the Ping to retrieve
        /// </summary>
        public int Id { get; init; }
    }
}