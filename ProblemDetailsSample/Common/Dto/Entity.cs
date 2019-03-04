using Ark.Tools.Core.EntityTag;

namespace ProblemDetailsSample.Common.Dto
{
    public static class Entity
    {
        public static class V1
        {
            public class Input : IEntityWithETag
            {
                public virtual string _ETag { get; set; }

                public string EntityId { get; set; }
            }

            public class Output : Input
            {
                public int Value { get; set; }
            }
        }

    }
}
