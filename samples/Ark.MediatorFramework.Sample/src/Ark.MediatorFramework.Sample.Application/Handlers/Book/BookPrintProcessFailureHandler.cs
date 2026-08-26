// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using NodaTime;

using Rebus.Handlers;
using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Persists the outcome of an exhausted print-completion notification.</summary>
public sealed class BookPrintProcessFailureHandler :
    IHandleMessages<Rebus.Retry.Simple.IFailed<ProcessBookPrintProcessRequest>>,
    ICommandHandler<MessagingFailed<ProcessBookPrintProcessRequest>>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IClock _clock;
    private readonly IContextProvider<ClaimsPrincipal> _user;

    /// <summary>Initializes a new instance of the <see cref="BookPrintProcessFailureHandler"/> class.</summary>
    /// <param name="factory">The application data context factory.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="user">The current user context.</param>
    public BookPrintProcessFailureHandler(
        ISampleDataContextFactory factory,
        IClock clock,
        IContextProvider<ClaimsPrincipal> user)
    {
        _factory = factory;
        _clock = clock;
        _user = user;
    }

    /// <inheritdoc />
    public async Task Handle(Rebus.Retry.Simple.IFailed<ProcessBookPrintProcessRequest> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _handleAsync(
            message.Message,
            message.Exceptions?.FirstOrDefault()?.Message,
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(
        MessagingFailed<ProcessBookPrintProcessRequest> command,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _handleAsync(
            command.Message,
            command.Exceptions[0].Message,
            ctk).ConfigureAwait(false);
    }

    private async Task _handleAsync(
        ProcessBookPrintProcessRequest message,
        string? errorMessage,
        CancellationToken ctk)
    {
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var process = await context.ReadBookPrintProcessAsync(
            message.Id,
            forUpdate: true,
            ctk: ctk).ConfigureAwait(false);
        if (process is null)
        {
            await context.CommitAsync(ctk).ConfigureAwait(false);
            return;
        }

        process = process with
        {
            Status = BookPrintProcessStatus.Error,
            ErrorMessage = errorMessage ?? "The print-completion notification was exhausted.",
        };
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = process.Id.ToString("D"),
            Operation = nameof(BookPrintProcessFailureHandler),
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.UpdateBookPrintProcessAsync(process, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
    }
}
