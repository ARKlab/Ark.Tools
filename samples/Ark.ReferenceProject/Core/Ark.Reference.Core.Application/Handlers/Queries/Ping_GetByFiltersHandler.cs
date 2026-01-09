using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Core;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

/// <summary>
/// Handler for retrieving Ping entities using filters with pagination
/// </summary>
public class Ping_GetByFiltersHandler : IQueryHandler<Ping_GetByFiltersQuery.V1, PagedResult<Ping.V1.Output>>
{
    private readonly ICoreDataContextFactory _coreDataContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ping_GetByFiltersHandler"/> class
    /// </summary>
    /// <param name="coreDataContext">The data context factory</param>
    public Ping_GetByFiltersHandler(ICoreDataContextFactory coreDataContext)
    {
        ArgumentNullException.ThrowIfNull(coreDataContext);

        _coreDataContext = coreDataContext;
    }

    /// <inheritdoc/>
    public PagedResult<Ping.V1.Output> Execute(Ping_GetByFiltersQuery.V1 query)
    {
        return ExecuteAsync(query).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<Ping.V1.Output>> ExecuteAsync(Ping_GetByFiltersQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);

        var (data, count) = await ctx.ReadPingByFiltersAsync(query, ctk).ConfigureAwait(false);

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