using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;

using System.Collections.Generic;

namespace Ark.Reference.Core.Common.Dto
{
    public static class PingSearchQueryDto
    {
        public record V1 : IQueryPaged
        {
            public int[] Id { get; init; } = [];
            public string[] Name { get; init; } = [];
            public PingType[] Type { get; init; } = [];

            public IEnumerable<string> Sort { get; set; } = [];
            public int Limit { get; init; } = 10;
            public int Skip { get; set; }
        }
    }
}