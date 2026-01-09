using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests;

/// <summary>
/// Handler for updating a Ping entity using PUT (full replacement)
/// </summary>
public class Ping_UpdatePutRequestHandler : IRequestHandler<Ping_UpdatePutRequest.V1, Ping.V1.Output?>
{
    private readonly ICoreDataContextFactory _coreDataContext;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ping_UpdatePutRequestHandler"/> class
    /// </summary>
    /// <param name="coreDataContext">The data context factory</param>
    /// <param name="userContext">The user context provider</param>
    public Ping_UpdatePutRequestHandler(
          ICoreDataContextFactory coreDataContext
          , IContextProvider<ClaimsPrincipal> userContext
        )
    {
        ArgumentNullException.ThrowIfNull(coreDataContext);
        ArgumentNullException.ThrowIfNull(userContext);

        _coreDataContext = coreDataContext;
        _userContext = userContext;
    }

    /// <inheritdoc/>
    public Ping.V1.Output? Execute(Ping_UpdatePutRequest.V1 request)
    {
        return ExecuteAsync(request).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<Ping.V1.Output?> ExecuteAsync(Ping_UpdatePutRequest.V1 request, CancellationToken ctk = default)
    {
        await using var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);

        var entity = await ctx.ReadPingByIdAsync(request.Id, ctk).ConfigureAwait(false);

        if (entity == null)
            return null;

        await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update put a Ping", ctk).ConfigureAwait(false);

        var updatePingData = new Ping.V1.Output()
        {
            Id = entity.Id,
            Name = request.Data?.Name,
            Type = request.Data?.Type,
            Code = $"PING_CODE_{request.Data?.Name}"
        };

        await ctx.PutPingAsync(updatePingData, ctk).ConfigureAwait(false);

        entity = await ctx.ReadPingByIdAsync(request.Id, ctk).ConfigureAwait(false);

        await ctx.CommitAsync(ctk).ConfigureAwait(false);

        return entity;
    }
}