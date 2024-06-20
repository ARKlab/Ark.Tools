using Ark.Tools.Core;
using Ark.Tools.Outbox;

using Core.Service.Common.Dto;
using Core.Service.Common.Enum;

using Ark.Reference.Common.Services.Audit;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Service.Application.DAL
{
    public interface ICoreDataContext : IOutboxContext, IContext, IDisposable, IAuditDataContext<AuditKind>
    {
        #region Ping
        Task<Ping.V1.Output> ReadPingByIdAsync(
              int id
            , CancellationToken ctk = default
        );

        Task<(IEnumerable<Ping.V1.Output> data, int count)> ReadPingByFiltersAsync(
              PingSearchQueryDto.V1 query
            , CancellationToken ctk = default
        );

        Task<int> InsertPingAsync(
              Ping.V1.Output entity
            , CancellationToken ctk = default
        );

        Task PutPingAsync(
            Ping.V1.Output entity
            , CancellationToken ctk = default
        );

        Task PatchPingAsync(
            Ping.V1.Output entity
            , CancellationToken ctk = default
        );

        Task DeletePingAsync(
            int id
            , CancellationToken ctk = default
        );

        Task<(AuditedEntityDto<Ping.V1.Output> pre, AuditedEntityDto<Ping.V1.Output> cur)> ReadPingAuditAsync(
            Guid auditId,
            CancellationToken ctk = default
        );
        #endregion
    }
}