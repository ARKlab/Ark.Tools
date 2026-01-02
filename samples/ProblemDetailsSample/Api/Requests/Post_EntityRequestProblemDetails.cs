using Ark.Tools.Solid;

using ProblemDetailsSample.Common.Dto;

namespace ProblemDetailsSample.Api.Requests
{
    public static class Post_EntityRequestProblemDetails
    {
        public class V1 : IRequest<Entity.V1.Output>
        {
            public string? EntityId { get; set; }
        }
    }
}