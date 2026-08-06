using Ark.Tools.Solid;

using NodaTime;

using ProblemDetailsSample.Common.Dto;

namespace ProblemDetailsSample.Api.Queries;

public static class Get_EntityByIdQuery
{
    public class V1 : IQuery<V1, Entity.V1.Output?>
    {
        public string? EntityId { get; set; }
        public Instant? AsOf { get; set; }
    }
}