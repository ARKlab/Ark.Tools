using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;

using System.Collections.Generic;

namespace Ark.Reference.Core.Common.Dto
{
    /// <summary>
    /// Search query DTO for BookPrintProcess filtering
    /// </summary>
    public static class BookPrintProcessSearchQueryDto
    {
        public record V1 : IQueryPaged
        {
            public int[] BookPrintProcessId { get; init; } = [];
            public int[] BookId { get; init; } = [];
            public BookPrintProcessStatus[] Status { get; init; } = [];

            public IEnumerable<string> Sort { get; set; } = [];
            public int Limit { get; init; } = 10;
            public int Skip { get; set; }
        }
    }
}