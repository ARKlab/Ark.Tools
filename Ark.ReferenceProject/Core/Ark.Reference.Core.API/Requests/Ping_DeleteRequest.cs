using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests
{
    public static class Ping_DeleteRequest
    {
        public class V1 : IRequest<bool>
        {
            public int Id { get; set; }
        }
    }
}
