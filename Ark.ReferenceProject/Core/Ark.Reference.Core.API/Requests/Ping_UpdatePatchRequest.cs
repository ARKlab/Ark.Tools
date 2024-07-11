using Ark.Tools.Solid;

using Ark.Reference.Core.Common.Dto;

namespace Ark.Reference.Core.API.Requests
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
