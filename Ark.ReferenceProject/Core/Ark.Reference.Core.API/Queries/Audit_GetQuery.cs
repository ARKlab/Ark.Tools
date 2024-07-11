using Ark.Tools.Core;
using Ark.Tools.Solid;

using Ark.Reference.Core.Common.Enum;

using Ark.Reference.Common.Services.Audit;

namespace Ark.Reference.Core.API.Queries
{
    public static class Audit_GetQuery
    {
        public class V1
            : AuditQueryDto.V1<AuditKind>
            , IQuery<PagedResult<AuditDto<AuditKind>>>
        {
        }
    }
}
