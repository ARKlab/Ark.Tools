// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Completes a queued book print process through Rebus.</summary>
public sealed class ProcessBookPrintProcessHandler :
    IRequestHandler<ProcessBookPrintProcessRequest, BookPrintProcessResponse>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;
    private readonly IPrintCompletedNotificationService _printCompletedNotificationService;

    /// <summary>Initializes a new instance of the <see cref="ProcessBookPrintProcessHandler"/> class.</summary>
    public ProcessBookPrintProcessHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock,
        IPrintCompletedNotificationService printCompletedNotificationService)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
        _printCompletedNotificationService = printCompletedNotificationService;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        ProcessBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var readContext = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var process = await readContext.ReadBookPrintProcessAsync(request.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book print process '{request.Id}' was not found.");
        await readContext.CommitAsync(ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
        {
            await _printCompletedNotificationService.NotifyAsync(process, ctk).ConfigureAwait(false);
            return process;
        }
        if (process.Status != BookPrintProcessStatus.Pending && process.Status != BookPrintProcessStatus.Running)
            return process;

        if (process.Status == BookPrintProcessStatus.Pending)
        {
            process = process with
            {
                Progress = 0.5,
                Status = BookPrintProcessStatus.Running,
            };
            process = await PersistAsync(process, ctk).ConfigureAwait(false);
        }

        process = process.ShouldFail
            ? process with
            {
                Status = BookPrintProcessStatus.Error,
                ErrorMessage = "The test book print process failed.",
            }
            : process with
            {
                Progress = 1,
                Status = BookPrintProcessStatus.Completed,
            };
        process = await PersistAsync(process, ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
            await _printCompletedNotificationService.NotifyAsync(process, ctk).ConfigureAwait(false);
        return process;
    }

    private async Task<BookPrintProcessResponse> PersistAsync(
        BookPrintProcessResponse process,
        CancellationToken ctk)
    {
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await context.WriteAuditAsync(CreateAudit(process.Id), ctk).ConfigureAwait(false);
        if (!await context.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book print process '{process.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }

    private AuditEntry CreateAudit(Guid id)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = id.ToString("D"),
            Operation = nameof(ProcessBookPrintProcessRequest),
            Timestamp = _clock.GetCurrentInstant(),
        };
    }
}
