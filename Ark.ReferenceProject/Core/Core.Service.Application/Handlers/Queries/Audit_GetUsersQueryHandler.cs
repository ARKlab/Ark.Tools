using Ark.Tools.Solid;

using Core.Service.API.Queries;
using Core.Service.Application.DAL;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Service.Application.Handlers.Queries
{
    internal class Audit_GetUsersQueryHandler : IQueryHandler<Audit_GetUsersQuery.V1, IEnumerable<string>>
    {
        private readonly Func<ICoreDataContext> _dataContext;

        public Audit_GetUsersQueryHandler(Func<ICoreDataContext> dataContext)
        {
            _dataContext = dataContext;
        }

        public IEnumerable<string> Execute(Audit_GetUsersQuery.V1 query)
        {
            return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<string>> ExecuteAsync(Audit_GetUsersQuery.V1 query, CancellationToken ctk = default)
        {
            using var ctx = _dataContext();
            return await ctx.ReadAuditUsersAsync(ctk: ctk);
        }
    }
}
