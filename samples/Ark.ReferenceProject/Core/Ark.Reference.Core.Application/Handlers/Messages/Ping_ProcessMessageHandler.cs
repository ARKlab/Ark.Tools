using Ark.Reference.Core.API.Messages;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;


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
            ArgumentNullException.ThrowIfNull(coreDataContext);
            ArgumentNullException.ThrowIfNull(userContext);

            _coreDataContext = coreDataContext;
            _userContext = userContext;
        }

        public async Task Handle(Ping_ProcessMessage.V1 message)
        {
            int currentMessageCount = MessageCounter.Increment();
            await using var ctx = await _coreDataContext.CreateAsync().ConfigureAwait(false);

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update Ping Async").ConfigureAwait(false);

            //** Check if exists InvoiceRun 
            var entity = await ctx.ReadPingByIdAsync(message.Id).ConfigureAwait(false);
            if (entity == null) return; // nothing to do ... been deleted?

            if (entity.Name?.Contains("fails", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (entity.Name.Contains("fast", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException($"FailFastEx_MsgCount_{currentMessageCount}");
                }

                throw new InvalidOperationException($"NormalEx_MsgCount_{currentMessageCount}");
            }

            await _updateEntityAndCommit(ctx, entity, $"HandleOk_MsgCount_{currentMessageCount}").ConfigureAwait(false);
        }

        public async Task Handle(IFailed<Ping_ProcessMessage.V1> message)
        {
            var ex = message.Exceptions?.FirstOrDefault();

            await _fail(message.Message, ex).ConfigureAwait(false);
        }

        private async Task _fail(Ping_ProcessMessage.V1 message, Rebus.Retry.ExceptionInfo? ex)
        {
            int currentMessageCount = MessageCounter.GetCount();

            await using var ctx = await _coreDataContext.CreateAsync().ConfigureAwait(false);
            var invoiceRun = await ctx.ReadPingByIdAsync(message.Id).ConfigureAwait(false);
            if (invoiceRun == null) return;

            await ctx.EnsureAudit(AuditKind.Ping, _userContext.GetUserId(), "Update Ping Async").ConfigureAwait(false);
            var e = ex?.Message;

            await _updateEntityAndCommit(ctx, invoiceRun, $"HandleFailed_{e ?? "<unknown>"}_MsgCount_{currentMessageCount}").ConfigureAwait(false);
        }

        private static async Task _updateEntityAndCommit(ICoreDataContext ctx, Ping.V1.Output entity, string code)
        {
            entity = entity with { Code = code };

            await ctx.PatchPingAsync(entity).ConfigureAwait(false);

            await ctx.CommitAsync().ConfigureAwait(false);
        }
    }
}