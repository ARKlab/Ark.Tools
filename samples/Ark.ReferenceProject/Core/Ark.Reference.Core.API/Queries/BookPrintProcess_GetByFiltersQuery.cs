using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries
{
    /// <summary>
    /// Query for retrieving BookPrintProcess entities by filters
    /// </summary>
    public static class BookPrintProcess_GetByFiltersQuery
    {
        public record V1 : IQuery<PagedResult<BookPrintProcess.V1.Output>>
        {
            public int[]? BookPrintProcessId { get; init; }
            public int[]? BookId { get; init; }
            public BookPrintProcessStatus[]? Status { get; init; }

            public string[]? Sort { get; init; }
            public int Skip { get; init; }
            public int Limit { get; init; }
        }
    }
}