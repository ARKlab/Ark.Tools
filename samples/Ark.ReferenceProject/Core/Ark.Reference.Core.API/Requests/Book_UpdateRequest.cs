using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests
{
    public static class Book_UpdateRequest
    {
        public record V1 : IRequest<Book.V1.Output?>
        {
            public int Id { get; init; }
            public Book.V1.Update? Data { get; init; }
        }
    }
}
