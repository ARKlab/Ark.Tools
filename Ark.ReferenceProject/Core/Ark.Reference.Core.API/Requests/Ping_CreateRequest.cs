using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests
{
    public static class Ping_CreateRequest
    {
        public record V1 : IRequest<Ping.V1.Output>
        {
            public Ping.V1.Create? Data { get; init; }
        }
    }
}
