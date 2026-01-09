using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries;

public static class Book_GetByFiltersQuery
{
    public record V1 : IQuery<PagedResult<Book.V1.Output>>
    {
        public int[]? Id { get; init; }
        public string[]? Title { get; init; }
        public string[]? Author { get; init; }
        public BookGenre[]? Genre { get; init; }

        public string[]? Sort { get; init; }
        public int Skip { get; init; }
        public int Limit { get; init; }
    }
}