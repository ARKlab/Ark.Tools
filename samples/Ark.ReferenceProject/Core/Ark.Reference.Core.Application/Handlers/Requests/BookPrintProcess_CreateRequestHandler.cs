using Ark.Reference.Core.API.Messages;
using Ark.Reference.Core.API.Requests;
using Ark.Reference.Core.Application.DAL;
using Ark.Reference.Core.Common.Dto;
using Ark.Reference.Core.Common.Enum;
using Ark.Reference.Core.Common.Exceptions;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Outbox.Rebus;
using Ark.Tools.Solid;

using Rebus.Bus;

using System.Security.Claims;

namespace Ark.Reference.Core.Application.Handlers.Requests;

public class BookPrintProcess_CreateRequestHandler : IRequestHandler<BookPrintProcess_CreateRequest.V1, BookPrintProcess.V1.Output>
{
    private readonly ICoreDataContextFactory _dataContextFactory;
    private readonly IContextProvider<ClaimsPrincipal> _userContext;
    private readonly IBus _bus;

    public BookPrintProcess_CreateRequestHandler(
        ICoreDataContextFactory dataContextFactory,
        IContextProvider<ClaimsPrincipal> userContext,
        IBus bus)
    {
        ArgumentNullException.ThrowIfNull(dataContextFactory);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(bus);

        _dataContextFactory = dataContextFactory;
        _userContext = userContext;
        _bus = bus;
    }

    /// <inheritdoc/>
    public BookPrintProcess.V1.Output Execute(BookPrintProcess_CreateRequest.V1 request)
    {
        return ExecuteAsync(request).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async Task<BookPrintProcess.V1.Output> ExecuteAsync(BookPrintProcess_CreateRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Data);

        await using var ctx = await _dataContextFactory.CreateAsync(ctk).ConfigureAwait(false);

        // Check if there's already a running or pending print process for this book
        var existingProcess = await ctx.ReadRunningPrintProcessForBookAsync(request.Data.BookId, ctk).ConfigureAwait(false);
        if (existingProcess != null)
        {
            throw new BusinessRuleViolationException(new BookPrintingProcessAlreadyRunningViolation(request.Data.BookId));
        }

        // Check if the book exists
        var book = await ctx.ReadBookByIdAsync(request.Data.BookId, ctk).ConfigureAwait(false);
        if (book == null)
        {
            throw new EntityNotFoundException($"Book with ID {request.Data.BookId} not found");
        }

        await ctx.EnsureAudit(AuditKind.BookPrintProcess, _userContext.GetUserId(), "Create BookPrintProcess", ctk).ConfigureAwait(false);

        var entity = new BookPrintProcess.V1.Output
        {
            BookPrintProcessId = 0,
            BookId = request.Data.BookId,
            Progress = 0.0,
            Status = BookPrintProcessStatus.Pending,
            ErrorMessage = null,
            ShouldFail = request.Data.ShouldFail,
            AuditId = ctx.CurrentAudit?.AuditId ?? Guid.Empty
        };

        var id = await ctx.PostBookPrintProcessAsync(entity, ctk).ConfigureAwait(false);
        entity = entity with { BookPrintProcessId = id };

        // Enlist message in outbox for transactional messaging
        using var scope = _bus.Enlist(ctx);

        await _bus.Send(new BookPrintProcess_StartMessage.V1
        {
            BookPrintProcessId = id,
            ShouldFail = request.Data.ShouldFail
        }).ConfigureAwait(false);

        await scope.CompleteAsync().ConfigureAwait(false);

        await ctx.CommitAsync(ctk).ConfigureAwait(false);

        return entity;
    }
}