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
    public class Ping_GetByFiltersHandler : IQueryHandler<Ping_GetByFiltersQuery.V1, PagedResult<Ping.V1.Output>>
    {
        private readonly ICoreDataContextFactory _coreDataContext;

        public Ping_GetByFiltersHandler(ICoreDataContextFactory coreDataContext)
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));

            _coreDataContext = coreDataContext;
        }

        public PagedResult<Ping.V1.Output> Execute(Ping_GetByFiltersQuery.V1 query)
        {
            return ExecuteAsync(query).GetAwaiter().GetResult();
        }

        public async Task<PagedResult<Ping.V1.Output>> ExecuteAsync(Ping_GetByFiltersQuery.V1 query, CancellationToken ctk = default)
        {
            EnsureArg.IsNotNull(query, nameof(query));

            await using var ctx = await _coreDataContext.CreateAsync(ctk);

            var (data, count) = await ctx.ReadPingByFiltersAsync(query, ctk);

            return new PagedResult<Ping.V1.Output>()
            {
                Count = count,
                Data = data,
                IsCountPartial = false,
                Limit = query.Limit,
                Skip = query.Skip
            };
        }

    }
}
