using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries
{
    public static class Book_GetByIdQuery
    {
        public record V1 : IQuery<Book.V1.Output?>
        {
            public int Id { get; init; }
        }
    }
}