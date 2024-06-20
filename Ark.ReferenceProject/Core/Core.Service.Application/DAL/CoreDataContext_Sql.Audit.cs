using System.Threading.Tasks;
using System.Threading;
using Core.Service.Common.Enum;
using Ark.Reference.Common.Services.Audit;
using System.Collections.Generic;

namespace Core.Service.Application.DAL
{
    public partial class CoreDataContext_Sql
    {
        //private const string _schemaAudit = "dbo";
        //private const string _tableAudit = "Audit";

        public Task<(IEnumerable<AuditDto<AuditKind>> records, int totalCount)> ReadAuditByFilterAsync(
              AuditQueryDto.V1<AuditKind> query
            , CancellationToken ctk = default)
        {
            return _auditContext.ReadAuditByFilterAsync(query, ctk);
        }

        public Task<IEnumerable<string>> ReadAuditUsersAsync(
            CancellationToken ctk = default)
        {
            return _auditContext.ReadAuditUsersAsync(ctk);
        }

        public AuditDto<AuditKind> CurrentAudit { get => _auditContext.CurrentAudit; }

        public ValueTask<AuditDto<AuditKind>> EnsureAudit(
            AuditKind kind
            , string userId
            , string infoMessage
            , CancellationToken ctk = default)
        {
            return _auditContext.EnsureAudit(kind, userId, infoMessage, ctk);
        }
    }
}
