using Ark.Tools.Solid;
using Ark.Reference.Common.Services.Audit.Dto;
using Ark.Reference.Common.Services.Audit;

namespace Ark.Reference.Core.API.Queries
{
    public class Audit_GetChangesQuery
    {
        public class V1
            : AuditChangesQueryDto.V1
            , IQuery<IAuditRecordReturn<IAuditEntity>>
        {
        }
    }
}
