using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;


namespace Ark.Reference.Core.Common.Dto;

public static class BookSearchQueryDto
{
    public record V1 : IQueryPaged
    {
        public int[] Id { get; init; } = [];
        public string[] Title { get; init; } = [];
        public string[] Author { get; init; } = [];
        public BookGenre[] Genre { get; init; } = [];

        public IEnumerable<string> Sort { get; set; } = [];
        public int Limit { get; init; } = 10;
        public int Skip { get; set; }
    }
}