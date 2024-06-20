using Ark.Tools.Solid;

using Core.Service.API.Requests;
using Core.Service.Application.DAL;
using Core.Service.Common.Dto;
using Core.Service.Common.Enum;

using EnsureThat;

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Service.Application.Handlers.Requests
{
    public class Ping_DeleteRequestHandler : IRequestHandler<Ping_DeleteRequest.V1, bool>
    {
        private readonly Func<ICoreDataContext> _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public Ping_DeleteRequestHandler(
              Func<ICoreDataContext> coreDataContext
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
            using var ctx = _coreDataContext();

            var entity = await ctx.ReadPingByIdAsync(request.Id, ctk);

            if (entity == null)
                return false;

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Delete existing Ping", ctk);

            await ctx.DeletePingAsync(request.Id, ctk);

            ctx.Commit();

            return true;
        }
    }
}
