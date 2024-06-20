using Ark.Tools.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Common.Services.Audit
{
    public interface IAuditDataContext<TAuditKind> : IContext, IDisposable
            where TAuditKind : struct, IConvertible
    {
        #region Audit
        Task<(IEnumerable<AuditDto<TAuditKind>> records, int totalCount)> ReadAuditByFilterAsync(
            AuditQueryDto.V1<TAuditKind> query
            , CancellationToken ctk = default
            );

        Task<IEnumerable<string>> ReadAuditUsersAsync(
            CancellationToken ctk = default
            );

        ValueTask<AuditDto<TAuditKind>> EnsureAudit(
            TAuditKind kind
            , string userId
            , string infoMessage
            , CancellationToken ctk = default
            );

        AuditDto<TAuditKind> CurrentAudit { get; }
        #endregion
    }
}
