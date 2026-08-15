using Ark.Tools.Solid;

using NodaTime;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Queries;

public static class Get_EntityByIdQuery
{
    public class V1 : IQuery<V1, Entity.V1.Output?>
    {
        public string? EntityId { get; set; }
        public Instant? AsOf { get; set; }
    }
}