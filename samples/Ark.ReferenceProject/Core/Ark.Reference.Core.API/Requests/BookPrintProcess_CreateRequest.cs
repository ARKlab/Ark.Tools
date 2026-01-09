using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests;

/// <summary>
/// Request to create a new book print process
/// </summary>
public static class BookPrintProcess_CreateRequest
{
    /// <summary>
    /// Version 1 of the create request
    /// </summary>
    public record V1 : IRequest<BookPrintProcess.V1.Output>
    {
        /// <summary>
        /// Gets or initializes the print process data
        /// </summary>
        public BookPrintProcess.V1.Create? Data { get; init; }
    }
}