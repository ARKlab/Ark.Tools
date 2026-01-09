using Ark.Tools.Solid;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Requests;

public static class Post_EntityRequestBusinessRuleViolation
{
    public class V1 : IRequest<Entity.V1.Output>
    {
        public string? EntityId { get; set; }
    }
}