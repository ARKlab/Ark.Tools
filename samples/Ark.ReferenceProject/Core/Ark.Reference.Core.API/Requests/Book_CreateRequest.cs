using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Requests
{
    public static class Book_CreateRequest
    {
        public record V1 : IRequest<Book.V1.Output>
        {
            public Book.V1.Create? Data { get; init; }
        }
    }
}