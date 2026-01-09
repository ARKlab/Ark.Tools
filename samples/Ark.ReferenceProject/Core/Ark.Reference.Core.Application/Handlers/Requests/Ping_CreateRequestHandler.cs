using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.Reference.Core.Application.Handlers.Requests;

/// <summary>
/// Handler for creating a new Ping entity
/// </summary>
public class Ping_CreateRequestHandler : IRequestHandler<Ping_CreateRequest.V1, Ping.V1.Output>
{
    private readonly ICoreDataContextFactory _coreDataContext;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ping_CreateRequestHandler"/> class
    /// </summary>
    /// <param name="coreDataContext">The data context factory</param>
    /// <param name="userContext">The user context provider</param>
    public Ping_CreateRequestHandler(
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
    public Ping.V1.Output Execute(Ping_CreateRequest.V1 request)
    {
        return ExecuteAsync(request).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<Ping.V1.Output> ExecuteAsync(Ping_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        await using var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);

        await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Create a new Ping", ctk).ConfigureAwait(false);

        var createPingData = new Ping.V1.Output()
        {
            Name = request.Data?.Name,
            Type = request.Data?.Type,
            Code = $"PING_CODE_{request.Data?.Name}"
        };

        var id = await ctx.InsertPingAsync(createPingData, ctk).ConfigureAwait(false);

        var entity = await ctx.ReadPingByIdAsync(id, ctk).ConfigureAwait(false);

        await ctx.CommitAsync(ctk).ConfigureAwait(false);

        return entity!;
    }
}