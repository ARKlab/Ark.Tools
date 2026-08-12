// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Outbox.Rebus;

using NodaTime;

using Rebus.Bus;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Starts a background print process for a persisted book.</summary>
public sealed class CreateBookPrintProcessHandler :
    IRequestHandler<CreateBookPrintProcessRequest, BookPrintProcessResponse>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IBus _bus;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CreateBookPrintProcessHandler"/> class.</summary>
    public CreateBookPrintProcessHandler(
        ISampleDataContextFactory factory,
        IBus bus,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _factory = factory;
        _bus = bus;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        CreateBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        if (await context.ReadBookAsync(request.BookId, ctk: ctk).ConfigureAwait(false) is null)
            throw new EntityNotFoundException($"Book '{request.BookId}' was not found.");

        var process = new BookPrintProcessResponse
        {
            Id = Guid.NewGuid(),
            BookId = request.BookId,
            Status = BookPrintProcessStatus.Pending,
            ShouldFail = request.ShouldFail,
        };
        if (!await context.TrySaveBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
            throw new BusinessRuleViolationException(new BookPrintingProcessAlreadyRunningViolation(request.BookId));
        await context.WriteAuditAsync(_createAudit(process.Id, nameof(CreateBookPrintProcessRequest)), ctk).ConfigureAwait(false);
        using var scope = _bus.Enlist(context.OutboxContext);
        await _bus.Send(new ProcessBookPrintProcessRequest { Id = process.Id }).ConfigureAwait(false);
        await scope.CompleteAsync().ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }

    private AuditEntry _createAudit(Guid id, string operation)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = id.ToString("D"),
            Operation = operation,
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}
