using Core.Service.Common.Enum;

using Ark.Reference.Common.Dto;

using System;
using System.Collections.Generic;

namespace Core.Service.Common.Dto
{
    public static class PingSearchQueryDto
    {
        public record V1 : IQueryPaged
        {
            public int[] Id { get; set; }
            public string[] Name { get; set; }
            public PingType[] Type { get; set; }

            public IEnumerable<string> Sort { get; set; } = Array.Empty<string>();
            public int Limit { get; set; } = 10;
            public int Skip { get; set; } = 0;
        }
    }
}
