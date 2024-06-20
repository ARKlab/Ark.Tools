using Ark.Tools.Core;
using Ark.Tools.Solid;

using Core.Service.Common.Dto;

namespace Core.Service.API.Queries
{
    public static class Ping_GetByFiltersQuery
    {
        public record V1 : PingSearchQueryDto.V1, IQuery<PagedResult<Ping.V1.Output>>
        {
        }
    }
}
