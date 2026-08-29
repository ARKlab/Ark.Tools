// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

using NodaTime;

using System.Diagnostics;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Completes or resumes a queued book print process.</summary>
public sealed class ProcessBookPrintProcessHandler :
    ICommandHandler<ProcessBookPrintProcessRequest>,
    IRequestHandler<ResumeBookPrintProcessRequest, BookPrintProcessResponse>
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

    /// <summary>Executes a queued book print process.</summary>
    /// <param name="request">The process request.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The resulting book print process.</returns>
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        ProcessBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _executeAsync(request.Id, ctk).ConfigureAwait(false);
    }

    /// <summary>Resumes a queued book print process through the request pipeline.</summary>
    /// <param name="request">The resume request.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>The resulting book print process.</returns>
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        ResumeBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _executeAsync(request.Id, ctk).ConfigureAwait(false);
    }

    private async Task<BookPrintProcessResponse> _executeAsync(
        Guid id,
        CancellationToken ctk)
    {
        using var activity = SampleTelemetry._activitySource.StartActivity(
            "ark.mediator.sample.book_print_process",
            ActivityKind.Consumer);
        activity?.SetTag("book_print_process.id", id);

        var readContext = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = readContext.ConfigureAwait(false);
        var process = await readContext.ReadBookPrintProcessAsync(id, forUpdate: true, ctk: ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book print process '{id}' was not found.");
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
            process = await _persistAsync(process, ctk).ConfigureAwait(false);
            if (process.Status != BookPrintProcessStatus.Running)
                return process;
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
        process = await _persistAsync(process, ctk).ConfigureAwait(false);
        if (process.Status == BookPrintProcessStatus.Completed)
            await _printCompletedNotificationService.NotifyAsync(process, ctk).ConfigureAwait(false);
        activity?.SetTag("book_print_process.status", process.Status.ToString());
        activity?.SetStatus(ActivityStatusCode.Ok);
        return process;
    }

    async Task ICommandHandler<ProcessBookPrintProcessRequest>.ExecuteAsync(
        ProcessBookPrintProcessRequest command,
        CancellationToken ctk)
    {
        _ = await ExecuteAsync(command, ctk).ConfigureAwait(false);
    }

    private async Task<BookPrintProcessResponse> _persistAsync(
        BookPrintProcessResponse process,
        CancellationToken ctk)
    {
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        if (!await context.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false))
        {
            var current = await context.ReadBookPrintProcessAsync(process.Id, ctk: ctk).ConfigureAwait(false);
            if (current is null)
                throw new EntityNotFoundException($"Book print process '{process.Id}' was not found.");

            await context.CommitAsync(ctk).ConfigureAwait(false);
            return current;
        }
        await context.WriteAuditAsync(_createAudit(process.Id), ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }

    private AuditEntry _createAudit(Guid id)
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
