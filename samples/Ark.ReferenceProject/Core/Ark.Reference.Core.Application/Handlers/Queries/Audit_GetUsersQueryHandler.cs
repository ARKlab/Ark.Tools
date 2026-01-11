using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

internal sealed class Audit_GetUsersQueryHandler : IQueryHandler<Audit_GetUsersQuery.V1, IEnumerable<string>>
{
    private readonly ICoreDataContextFactory _dataContext;

    public Audit_GetUsersQueryHandler(ICoreDataContextFactory dataContext)
    {
        _dataContext = dataContext;
    }

    public IEnumerable<string> Execute(Audit_GetUsersQuery.V1 query)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(query).ConfigureAwait(true).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    public async Task<IEnumerable<string>> ExecuteAsync(Audit_GetUsersQuery.V1 query, CancellationToken ctk = default)
    {
        var ctx = await _dataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);
        return await ctx.ReadAuditUsersAsync(ctk: ctk).ConfigureAwait(false);
    }
}
