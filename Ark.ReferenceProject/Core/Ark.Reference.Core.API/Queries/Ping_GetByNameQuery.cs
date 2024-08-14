using Ark.Tools.Solid;

using Ark.Reference.Core.Common.Dto;

namespace Ark.Reference.Core.API.Queries
{
    public static class Ping_GetByNameQuery
    {
        public record V1 : IQuery<Ping.V1.Output>
        {
            public string? Name { get; init; }
        }
    }
}
