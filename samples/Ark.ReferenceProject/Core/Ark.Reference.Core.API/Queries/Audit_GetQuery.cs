using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries
{
    public static class Audit_GetQuery
    {
        public record V1
            : AuditQueryDto.V1<AuditKind>
            , IQuery<PagedResult<AuditDto<AuditKind>>>
        {
        }
    }
}