using Ark.Tools.Solid;

using Core.Service.Common.Dto;

namespace Core.Service.API.Requests
{
    public static class Ping_UpdatePatchRequest
    {
        public record V1 : IRequest<Ping.V1.Output>
        {
            public int Id { get; set; }
            public Ping.V1.Update Data { get; set; }
        }
    }
}
