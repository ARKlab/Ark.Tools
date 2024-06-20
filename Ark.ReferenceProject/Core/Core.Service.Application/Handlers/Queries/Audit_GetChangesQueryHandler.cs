using Ark.Tools.Core;
using Ark.Tools.Solid;

using Core.Service.API.Queries;
using Core.Service.Application.DAL;
using Core.Service.Common.Dto;
using Core.Service.Common.Enum;

using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Common.Services.Audit.Dto;

using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Service.Application.Handlers.Queries
{
    public class Audit_GetChangesQueryHandler : IQueryHandler<Audit_GetChangesQuery.V1, IAuditRecordReturn<IAuditEntity>>
    {
        private readonly Func<ICoreDataContext> _dataContext;

        public Audit_GetChangesQueryHandler(Func<ICoreDataContext> dataContext)
        {
            _dataContext = dataContext;
        }

        public IAuditRecordReturn<IAuditEntity> Execute(Audit_GetChangesQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<IAuditRecordReturn<IAuditEntity>> ExecuteAsync(Audit_GetChangesQuery.V1 query, CancellationToken ctk = default)
        {
            using var ctx = _dataContext();

            var (records, count) = await ctx.ReadAuditByFilterAsync(new Audit_GetQuery.V1 { AuditIds = [query.AuditId] }, ctk);

            if (count == 0)
                throw new EntityNotFoundException($"Audit with AuditId {query.AuditId} not found");

            if (count != 1)
                throw new ApplicationException($"Multiple Audits found for AuditId {query.AuditId}");

            var audit = records.Single();

            switch (audit.Kind)
            {
                //Implement audit here....

                case AuditKind.Ping:
                    return AuditRecordReturn.V1<Ping.V1.Output>.From(
                        await ctx.ReadPingAuditAsync(query.AuditId, ctk)
                    );

                default:
                    throw new ApplicationException($"AuditKind {audit.Kind} is not supported");
            }
        }
    }
}
