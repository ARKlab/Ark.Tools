using Ark.Tools.Solid;

namespace Core.Service.API.Requests
{
    public static class Ping_DeleteRequest
    {
        public class V1 : IRequest<bool>
        {
            public int Id { get; set; }
        }
    }
}
