using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using EnsureThat;

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests
{
    public class Ping_DeleteRequestHandler : IRequestHandler<Ping_DeleteRequest.V1, bool>
    {
        private readonly ICoreDataContextFactory _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public Ping_DeleteRequestHandler(
              ICoreDataContextFactory coreDataContext
              , IContextProvider<ClaimsPrincipal> userContext
            )
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));
            EnsureArg.IsNotNull(userContext, nameof(userContext));

            _coreDataContext = coreDataContext;
            _userContext = userContext;
        }

        public bool Execute(Ping_DeleteRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync(Ping_DeleteRequest.V1 request, CancellationToken ctk = default)
        {
            await using var ctx = await _coreDataContext.CreateAsync(ctk);

            var entity = await ctx.ReadPingByIdAsync(request.Id, ctk);

            if (entity == null)
                return false;

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Delete existing Ping", ctk);

            await ctx.DeletePingAsync(request.Id, ctk);

            await ctx.CommitAsync(ctk);

            return true;
        }
    }
}
