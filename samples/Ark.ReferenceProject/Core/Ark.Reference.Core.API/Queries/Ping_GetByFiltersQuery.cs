using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries;

public static class Ping_GetByFiltersQuery
{
    public record V1 : PingSearchQueryDto.V1, IQuery<V1, PagedResult<Ping.V1.Output>>
    {
    }
}