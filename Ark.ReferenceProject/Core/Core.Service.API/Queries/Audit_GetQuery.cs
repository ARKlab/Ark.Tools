using Ark.Tools.Core;
using Ark.Tools.Solid;

using Core.Service.Common.Enum;

using Ark.Reference.Common.Services.Audit;

namespace Core.Service.API.Queries
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
