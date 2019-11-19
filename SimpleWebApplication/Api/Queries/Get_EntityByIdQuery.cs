using Ark.Tools.Solid;
using NodaTime;
using SimpleWebApplication.Dto;

namespace SimpleWebApplication.Api.Queries
{
    public static class Get_EntityByIdQuery
    {
        public class V1 : IQuery<Entity.V1.Output>
        {
            public string EntityId { get; set; }
            public Instant? AsOf { get; set; }
        }
    }
}
