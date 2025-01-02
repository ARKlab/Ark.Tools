using Ark.Reference.Core.API.Messages;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using EnsureThat;

using Rebus.Retry.Simple;

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Ark.Reference.Core.Application.Handlers.Messages
{
    public class Ping_ProcessMessageHandler
                : IHandleMessagesCore<Ping_ProcessMessage.V1>
                , IHandleMessagesCore<IFailed<Ping_ProcessMessage.V1>>
    {
        private readonly ICoreDataContextFactory _coreDataContext;
        private readonly IContextProvider<ClaimsPrincipal> _userContext;

        public Ping_ProcessMessageHandler(
              ICoreDataContextFactory coreDataContext
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
            int currentMessageCount = MessageCounter.Increment();
            await using var ctx = await _coreDataContext.CreateAsync();

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update Ping Async");

            //** Check if exists InvoiceRun 
            var entity = await ctx.ReadPingByIdAsync(message.Id);
            if (entity == null) return; // nothing to do ... been deleted?

            if (entity.Name?.Contains("fails", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                if (entity.Name.Contains("fast", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new NotSupportedException($"FailFastEx_MsgCount_{currentMessageCount}");
                }

                throw new InvalidOperationException($"NormalEx_MsgCount_{currentMessageCount}");
            }

            await _updateEntityAndCommit(ctx, entity, $"HandleOk_MsgCount_{currentMessageCount}");
        }

        public async Task Handle(IFailed<Ping_ProcessMessage.V1> message)
        {
            var ex = message.Exceptions?.FirstOrDefault();

            await _fail(message.Message, ex);
        }

        private async Task _fail(Ping_ProcessMessage.V1 message, Rebus.Retry.ExceptionInfo? ex)
        {
            int currentMessageCount = MessageCounter.GetCount();

            await using var ctx = await _coreDataContext.CreateAsync();
            var invoiceRun = await ctx.ReadPingByIdAsync(message.Id);
            if (invoiceRun == null) return;

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update Ping Async");
            var e = ex?.Message;

            await _updateEntityAndCommit(ctx, invoiceRun, $"HandleFailed_{e ?? "<unknown>"}_MsgCount_{currentMessageCount}");
        }

        private async Task _updateEntityAndCommit(ICoreDataContext ctx, Ping.V1.Output entity, string code)
        {
            entity = entity with { Code = code };

            await ctx.PatchPingAsync(entity);

            await ctx.CommitAsync();
        }
    }
}