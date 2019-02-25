using Ark.Tools.Core.EntityTag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

            }
        }

    }
}
