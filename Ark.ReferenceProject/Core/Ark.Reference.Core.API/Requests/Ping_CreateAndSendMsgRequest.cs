using Ark.Tools.Solid;

using Ark.Reference.Core.Common.Dto;

namespace Ark.Reference.Core.API.Requests
{
    public static class Ping_CreateAndSendMsgRequest
    {
        public record V1 : IRequest<Ping.V1.Output>
        {
            public Ping.V1.Create Data { get; set; }
        }
    }
}
