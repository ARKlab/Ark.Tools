using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

/// <summary>
/// Handler for retrieving a Ping entity by its ID
/// </summary>
public class Ping_GetIdHandler : IQueryHandler<Ping_GetByIdQuery.V1, Ping.V1.Output?>
{
    private readonly ICoreDataContextFactory _coreDataContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ping_GetIdHandler"/> class
    /// </summary>
    /// <param name="coreDataContext">The data context factory</param>
    public Ping_GetIdHandler(ICoreDataContextFactory coreDataContext)
    {
        ArgumentNullException.ThrowIfNull(coreDataContext);

        _coreDataContext = coreDataContext;
    }

    /// <inheritdoc/>
    public Ping.V1.Output? Execute(Ping_GetByIdQuery.V1 query)
    {
        return ExecuteAsync(query).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<Ping.V1.Output?> ExecuteAsync(Ping_GetByIdQuery.V1 query, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        var entity = await ctx.ReadPingByIdAsync(query.Id, ctk).ConfigureAwait(false);

        return entity;
    }

}