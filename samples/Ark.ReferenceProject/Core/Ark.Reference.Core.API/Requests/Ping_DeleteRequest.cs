using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests
{
    /// <summary>
    /// Request to delete a Ping entity
    /// </summary>
    public static class Ping_DeleteRequest
    {
        /// <summary>
        /// Version 1 of the delete request
        /// </summary>
        public record V1 : IRequest<bool>
        {
            /// <summary>
            /// Gets or initializes the ID of the Ping to delete
            /// </summary>
            public int Id { get; init; }
        }
    }
}