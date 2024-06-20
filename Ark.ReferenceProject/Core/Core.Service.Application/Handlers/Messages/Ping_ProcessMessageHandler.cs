using Ark.Tools.Solid;

using Core.Service.Common.Enum;

using EnsureThat;
using Rebus.Retry.Simple;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Core.Service.API.Messages;
using Core.Service.Application.DAL;
using Core.Service.Common.Dto;

namespace Core.Service.Application.Handlers.Messages
{
    public class Ping_ProcessMessageHandler
                : IHandleMessagesCore<Ping_ProcessMessage.V1>
                , IHandleMessagesCore<IFailed<Ping_ProcessMessage.V1>>
    {
        private readonly Func<ICoreDataContext> _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;


        public Ping_ProcessMessageHandler(
              Func<ICoreDataContext> coreDataContext
            , IContextProvider<ClaimsPrincipal> userContext
            )
        {
            EnsureArg.IsNotNull(coreDataContext, nameof(coreDataContext));
            EnsureArg.IsNotNull(userContext, nameof(userContext));

            _coreDataContext = coreDataContext;
            _userContext = userContext;
        }

        public async Task Handle(Ping_ProcessMessage.V1 message)
        {
            using var ctx = _coreDataContext();

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update Ping Async");

            //** Check if exists InvoiceRun 
            var entity = await ctx.ReadPingByIdAsync(message.Id);

            await _updateEntityAndCommit(ctx, entity, "HandleOk");
        }

        public async Task Handle(IFailed<Ping_ProcessMessage.V1> message)
        {
            var ex = message.Exceptions?.FirstOrDefault();

            await _fail(message.Message, ex);
        }

        private async Task _fail(Ping_ProcessMessage.V1 message, Rebus.Retry.ExceptionInfo ex)
        {
            using var ctx = _coreDataContext();
            var invoiceRun = await ctx.ReadPingByIdAsync(message.Id);

            var e = ex.Message;

            await _updateEntityAndCommit(ctx, invoiceRun, "HandleFailed");
        }

        private async Task _updateEntityAndCommit(ICoreDataContext ctx, Ping.V1.Output entity, string code)
        {
            entity.Code = code;

            await ctx.PatchPingAsync(entity);

            ctx.Commit();
        }
    }
}