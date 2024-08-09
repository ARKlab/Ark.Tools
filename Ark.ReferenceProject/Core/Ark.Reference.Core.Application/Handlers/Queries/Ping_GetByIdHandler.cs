using Ark.Tools.Solid;

using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;

using EnsureThat;

using System;
using System.Threading;
using System.Threading.Tasks;

using Ark.Tools.Core;

namespace Core.Services.Application.Handlers.Queries
{
    public class Ping_GetIdHandler : IQueryHandler<Ping_GetByIdQuery.V1, Ping.V1.Output>
    {
        private readonly ICoreDataContextFactory _coreDataContext;

        public Ping_GetIdHandler(ICoreDataContextFactory coreDataContext)
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));

            _coreDataContext = coreDataContext;
        }

        public Ping.V1.Output Execute(Ping_GetByIdQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<Ping.V1.Output> ExecuteAsync(Ping_GetByIdQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));

            await using var ctx = await _coreDataContext.CreateAsync(ctk);

            var entity = await ctx.ReadPingByIdAsync(query.Id, ctk);

            return entity;
        }

    }
}
