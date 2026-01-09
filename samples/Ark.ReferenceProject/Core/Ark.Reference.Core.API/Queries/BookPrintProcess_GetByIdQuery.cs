using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries;

/// <summary>
/// Query for retrieving a BookPrintProcess by ID
/// </summary>
public static class BookPrintProcess_GetByIdQuery
{
    public record V1 : IQuery<BookPrintProcess.V1.Output?>
    {
        public int BookPrintProcessId { get; init; }
    }
}