using Ark.Reference.Common.Services.Audit;
using Ark.Reference.Common.Services.Audit.Dto;
using Ark.Reference.Core.API.Queries;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Core;
using Ark.Tools.Solid;


namespace Ark.Reference.Core.Application.Handlers.Queries;

public class Audit_GetChangesQueryHandler : IQueryHandler<Audit_GetChangesQuery.V1, IAuditRecordReturn<IAuditEntity>>
{
    private readonly ICoreDataContextFactory _dataContext;

    public Audit_GetChangesQueryHandler(ICoreDataContextFactory dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<IAuditRecordReturn<IAuditEntity>> ExecuteAsync(Audit_GetChangesQuery.V1 query, CancellationToken ctk = default)
    {
        var ctx = await _dataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        var (records, count) = await ctx.ReadAuditByFilterAsync(new Audit_GetQuery.V1 { AuditIds = [query.AuditId] }, ctk).ConfigureAwait(false);

        if (count == 0)
            throw new EntityNotFoundException($"Audit with AuditId {query.AuditId} not found");

        var audit = records.Single();

        switch (audit.Kind)
        {
            //Implement audit here....

            case AuditKind.Ping:
                return AuditRecordReturn.V1<Ping.V1.Output>.From(
                    (await ctx.ReadPingAuditAsync(query.AuditId, ctk).ConfigureAwait(false))!
                );

            default:
                throw new NotSupportedException($"AuditKind {audit.Kind} is not supported");
        }
    }
}
