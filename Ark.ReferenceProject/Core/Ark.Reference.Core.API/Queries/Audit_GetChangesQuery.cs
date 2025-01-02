using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Common.Services.Audit.Dto;
using Ark.Tools.Solid;

namespace Ark.Reference.Core.API.Queries
{
    public static class Audit_GetChangesQuery
    {
        public record V1
            : AuditChangesQueryDto.V1
            , IQuery<IAuditRecordReturn<IAuditEntity>>
        {
        }
    }
}
