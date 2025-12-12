using Ark.Tools.Solid;

using WebApplicationDemo.Dto;

namespace WebApplicationDemo.Api.Requests
{
    public static class Post_EntityRequest
    {
        public record V1 : Entity.V1.Input, IRequest<Entity.V1.Output>
        {
            public V1() { }
            public V1(Entity.V1.Input input)
            {
                EntityId = input.EntityId;
                _ETag = input._ETag;
                Strings = input.Strings;
                Ts = input.Ts;
                EntityResult = input.EntityResult;
                EntityTest = input.EntityTest;
            }
        }
    }
}
