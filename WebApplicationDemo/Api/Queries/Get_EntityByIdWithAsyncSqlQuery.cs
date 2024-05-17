using Ark.Tools.Solid;
using NodaTime;
using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Queries
{
    public static class Get_EntityByIdWithAsyncSqlQuery
    {
        public class V1 : IQuery<Person?>
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? Type { get; set; }
            public int? Count { get; set; }
        }
    }
}
