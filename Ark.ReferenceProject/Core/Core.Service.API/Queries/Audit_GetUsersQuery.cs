using Ark.Tools.Solid;

using System.Collections.Generic;

namespace Core.Service.API.Queries
{
    public class Audit_GetUsersQuery
    {
        public class V1 : IQuery<IEnumerable<string>>
        {
        }
    }
}
