using Ark.Tools.Core;
using Ark.Tools.Solid;

using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Enum;

using Ark.Reference.Common.Services.Audit;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Queries
{
    internal class Audit_GetQueryHandler : IQueryHandler<Audit_GetQuery.V1, PagedResult<AuditDto<AuditKind>>>
    {
        private readonly Func<ICoreDataContext> _dataContext;

        public Audit_GetQueryHandler(Func<ICoreDataContext> dataContext)
        {
            _dataContext = dataContext;
        }

        public PagedResult<AuditDto<AuditKind>> Execute(Audit_GetQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<PagedResult<AuditDto<AuditKind>>> ExecuteAsync(Audit_GetQuery.V1 query, CancellationToken ctk = default)
        {
            using var ctx = _dataContext();

            var (records, count) = await ctx.ReadAuditByFilterAsync(query, ctk: ctk);

            return new PagedResult<AuditDto<AuditKind>>()
            {
                Count = count,
                IsCountPartial = false,
                Data = records,
                Skip = query.Skip,
                Limit = query.Limit
            };
        }
    }
}
