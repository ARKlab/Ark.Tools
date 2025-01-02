using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries
{
    public static class Ping_GetByIdQuery
    {
        public record V1 : IQuery<Ping.V1.Output?>
        {
            public int Id { get; init; }
        }
    }
}
