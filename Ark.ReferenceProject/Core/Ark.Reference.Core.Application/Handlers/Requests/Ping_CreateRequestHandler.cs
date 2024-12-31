using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using EnsureThat;

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Requests
{
    public class Ping_CreateRequestHandler : IRequestHandler<Ping_CreateRequest.V1, Ping.V1.Output>
    {
        private readonly ICoreDataContextFactory _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public Ping_CreateRequestHandler(
              ICoreDataContextFactory coreDataContext
              , IContextProvider<ClaimsPrincipal> userContext
            )
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));
            EnsureArg.IsNotNull(userContext, nameof(userContext));

            _coreDataContext = coreDataContext;
            _userContext = userContext;
        }

        public Ping.V1.Output Execute(Ping_CreateRequest.V1 request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        public async Task<Ping.V1.Output> ExecuteAsync(Ping_CreateRequest.V1 request, CancellationToken ctk = default)
        {
            await using var ctx = await _coreDataContext.CreateAsync(ctk);

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Create a new Ping", ctk);

            var createPingData = new Ping.V1.Output()
            {
                Name = request.Data?.Name,
                Type = request.Data?.Type,
                Code = $"PING_CODE_{request.Data?.Name}"
            };

            var id = await ctx.InsertPingAsync(createPingData, ctk);

            var entity = await ctx.ReadPingByIdAsync(id, ctk);

            await ctx.CommitAsync(ctk);

            return entity!;
        }
    }
}
