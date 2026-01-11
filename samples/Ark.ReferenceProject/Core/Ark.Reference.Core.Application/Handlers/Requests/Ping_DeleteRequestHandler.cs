using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using System.Security.Claims;

namespace Ark.Reference.Core.Application.Handlers.Requests;

/// <summary>
/// Handler for deleting a Ping entity
/// </summary>
public class Ping_DeleteRequestHandler : IRequestHandler<Ping_DeleteRequest.V1, bool>
{
    private readonly ICoreDataContextFactory _coreDataContext;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="Ping_DeleteRequestHandler"/> class
    /// </summary>
    /// <param name="coreDataContext">The data context factory</param>
    /// <param name="userContext">The user context provider</param>
    public Ping_DeleteRequestHandler(
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
    public bool Execute(Ping_DeleteRequest.V1 request)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return ExecuteAsync(request).GetAwaiter().GetResult();
#pragma warning restore CS0618 // Type or member is obsolete
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteAsync(Ping_DeleteRequest.V1 request, CancellationToken ctk = default)
    {
        var ctx = await _coreDataContext.CreateAsync(ctk).ConfigureAwait(false);
        await using var _ = ctx.ConfigureAwait(false);

        var entity = await ctx.ReadPingByIdAsync(request.Id, ctk).ConfigureAwait(false);

        if (entity == null)
            return false;

        await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Delete existing Ping", ctk).ConfigureAwait(false);

        await ctx.DeletePingAsync(request.Id, ctk).ConfigureAwait(false);

        await ctx.CommitAsync(ctk).ConfigureAwait(false);

        return true;
    }
}
