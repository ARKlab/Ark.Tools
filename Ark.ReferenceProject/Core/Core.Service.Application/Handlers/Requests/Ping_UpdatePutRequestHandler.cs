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
    public class Ping_UpdatePutRequestHandler : IRequestHandler<Ping_UpdatePutRequest.V1, Ping.V1.Output>
    {
        private readonly Func<ICoreDataContext> _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public Ping_UpdatePutRequestHandler(
              Func<ICoreDataContext> coreDataContext
              , IContextProvider<ClaimsPrincipal> userContext
            )
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));
            EnsureArg.IsNotNull(userContext, nameof(userContext));

            _coreDataContext = coreDataContext;
            _userContext = userContext;
        }

        public Ping.V1.Output Execute(Ping_UpdatePutRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        public async Task<Ping.V1.Output> ExecuteAsync(Ping_UpdatePutRequest.V1 request, CancellationToken ctk = default)
        {
            using var ctx = _coreDataContext();

            var entity = await ctx.ReadPingByIdAsync(request.Id, ctk);

            if (entity == null)
                return null;

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update put a Ping", ctk);

            var updatePingData = new Ping.V1.Output()
            {
                Id = entity.Id,
                Name = request.Data.Name,
                Type = request.Data.Type,
                Code = $"PING_CODE_{request.Data.Name}"
            };

            await ctx.PutPingAsync(updatePingData, ctk);

            entity = await ctx.ReadPingByIdAsync(request.Id, ctk);

            ctx.Commit();

            return entity;
        }
    }
}
