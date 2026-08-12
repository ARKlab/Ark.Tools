using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests;

/// <summary>
/// Defines the request used to create multiple books in one operation.
/// </summary>
public static class Book_BulkCreateRequest
{
    /// <summary>
    /// Version 1 of the bulk create request.
    /// </summary>
    public record V1 : IRequest<V1, IEnumerable<Book.V1.Output>>
    {
        /// <summary>
        /// Gets or initializes the books to create.
        /// </summary>
        public IEnumerable<Book.V1.Create>? Data { get; init; }
    }
}
