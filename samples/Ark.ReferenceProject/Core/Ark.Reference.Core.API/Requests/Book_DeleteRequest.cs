using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests
{
    public static class Book_DeleteRequest
    {
        public record V1 : IRequest<bool>
        {
            public int Id { get; init; }
        }
    }
}
