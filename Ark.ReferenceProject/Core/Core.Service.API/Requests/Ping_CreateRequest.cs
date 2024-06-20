using Ark.Tools.Solid;

using Core.Service.Common.Dto;

namespace Core.Service.API.Requests
{
    public static class Ping_CreateRequest
    {
        public record V1 : IRequest<Ping.V1.Output>
        {
            public Ping.V1.Create Data { get; set; }
        }
    }
}
