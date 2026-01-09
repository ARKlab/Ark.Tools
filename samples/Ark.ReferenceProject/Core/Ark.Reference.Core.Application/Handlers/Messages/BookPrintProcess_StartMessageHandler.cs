using Ark.Reference.Core.API.Messages;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Enum;
using Ark.Tools.Solid;

using Rebus.Retry.Simple;

using System.Security.Claims;

namespace Ark.Reference.Core.Application.Handlers.Messages;

public class BookPrintProcess_StartMessageHandler
    : IHandleMessagesCore<BookPrintProcess_StartMessage.V1>
    , IHandleMessagesCore<IFailed<BookPrintProcess_StartMessage.V1>>
{
    private readonly ICoreDataContextFactory _dataContextFactory;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;

    public BookPrintProcess_StartMessageHandler(
        ICoreDataContextFactory dataContextFactory,
        IContextProvider<ClaimsPrincipal> userContext)
    {
        ArgumentNullException.ThrowIfNull(dataContextFactory);
        ArgumentNullException.ThrowIfNull(userContext);

        _dataContextFactory = dataContextFactory;
        _userContext = userContext;
    }

    public async Task Handle(BookPrintProcess_StartMessage.V1 message)
    {
        await using var ctx = await _dataContextFactory.CreateAsync().ConfigureAwait(false);

        var process = await ctx.ReadBookPrintProcessByIdAsync(message.BookPrintProcessId).ConfigureAwait(false);
        if (process == null) return; // Process was deleted

        await ctx.EnsureAudit(AuditKind.BookPrintProcess, _userContext.GetUserId(), "Process BookPrintProcess").ConfigureAwait(false);

        // Update status to Running if it's Pending
        if (process.Status == BookPrintProcessStatus.Pending)
        {
            process = process with
            {
                Status = BookPrintProcessStatus.Running,
                AuditId = ctx.CurrentAudit?.AuditId ?? Guid.Empty
            };
            await ctx.PutBookPrintProcessAsync(process).ConfigureAwait(false);
            await ctx.CommitAsync().ConfigureAwait(false);
        }

        // Simulate progressive printing: 10% each step
        // Note: Using short delays for integration test compatibility
        for (int i = 1; i <= 10; i++)
        {
            // Simulate work (short delay for test performance)
            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

            var newProgress = i * 0.1;

            // Check for error scenario at 30% progress
            if (message.ShouldFail && newProgress >= 0.3)
            {
                throw new InvalidOperationException("Simulated print process failure at 30% progress");
            }

            // Update progress
            await using var updateCtx = await _dataContextFactory.CreateAsync().ConfigureAwait(false);
            var currentProcess = await updateCtx.ReadBookPrintProcessByIdAsync(message.BookPrintProcessId).ConfigureAwait(false);
            if (currentProcess == null) return; // Process was deleted

            await updateCtx.EnsureAudit(AuditKind.BookPrintProcess, _userContext.GetUserId(), "Update BookPrintProcess Progress").ConfigureAwait(false);

            currentProcess = currentProcess with
            {
                Progress = newProgress,
                Status = newProgress >= 1.0 ? BookPrintProcessStatus.Completed : BookPrintProcessStatus.Running,
                AuditId = updateCtx.CurrentAudit?.AuditId ?? Guid.Empty
            };

            await updateCtx.PutBookPrintProcessAsync(currentProcess).ConfigureAwait(false);
            await updateCtx.CommitAsync().ConfigureAwait(false);
        }
    }

    public async Task Handle(IFailed<BookPrintProcess_StartMessage.V1> message)
    {
        var ex = message.Exceptions?.FirstOrDefault();

        await using var ctx = await _dataContextFactory.CreateAsync().ConfigureAwait(false);

        var process = await ctx.ReadBookPrintProcessByIdAsync(message.Message.BookPrintProcessId).ConfigureAwait(false);
        if (process == null) return; // Process was deleted

        await ctx.EnsureAudit(AuditKind.BookPrintProcess, _userContext.GetUserId(), "Fail BookPrintProcess").ConfigureAwait(false);

        process = process with
        {
            Status = BookPrintProcessStatus.Error,
            ErrorMessage = ex?.ToString() ?? "Unknown error",
            AuditId = ctx.CurrentAudit?.AuditId ?? Guid.Empty
        };

        await ctx.PutBookPrintProcessAsync(process).ConfigureAwait(false);
        await ctx.CommitAsync().ConfigureAwait(false);
    }
}