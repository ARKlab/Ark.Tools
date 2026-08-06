using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;

namespace ProblemDetailsSample.Api.Requests;

public static class Post_EntityRequestBusinessRuleViolation
{
    public class V1 : IRequest<V1, Entity.V1.Output>
    {
        public string? EntityId { get; set; }
    }
}