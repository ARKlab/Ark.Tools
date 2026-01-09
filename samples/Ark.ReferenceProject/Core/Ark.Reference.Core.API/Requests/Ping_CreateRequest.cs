using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests;

/// <summary>
/// Request to create a new Ping entity
/// </summary>
public static class Ping_CreateRequest
{
    /// <summary>
    /// Version 1 of the create request
    /// </summary>
    public record V1 : IRequest<Ping.V1.Output>
    {
        /// <summary>
        /// Gets or initializes the creation data for the Ping
        /// </summary>
        public Ping.V1.Create? Data { get; init; }
    }
}