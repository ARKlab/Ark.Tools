using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;

using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Queries
{
    internal sealed class Audit_GetQueryHandler : IQueryHandler<Audit_GetQuery.V1, PagedResult<AuditDto<AuditKind>>>
    {
        private readonly ICoreDataContextFactory _dataContext;

        public Audit_GetQueryHandler(ICoreDataContextFactory dataContext)
        {
            _dataContext = dataContext;
        }

        public PagedResult<AuditDto<AuditKind>> Execute(Audit_GetQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<PagedResult<AuditDto<AuditKind>>> ExecuteAsync(Audit_GetQuery.V1 query, CancellationToken ctk = default)
        {
            await using var ctx = await _dataContext.CreateAsync(ctk).ConfigureAwait(false);

            var (records, count) = await ctx.ReadAuditByFilterAsync(query, ctk: ctk).ConfigureAwait(false);

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