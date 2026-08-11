// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using NodaTime;

using Rebus.Handlers;
using Rebus.Retry.Simple;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Persists the outcome of an exhausted print-completion notification.</summary>
public sealed class BookPrintProcessFailureHandler : IHandleMessages<IFailed<ProcessBookPrintProcessRequest>>
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
    public async Task Handle(IFailed<ProcessBookPrintProcessRequest> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var context = await _factory.CreateAsync().ConfigureAwait(false);
        var process = await context.ReadBookPrintProcessAsync(message.Message.Id).ConfigureAwait(false);
        if (process is null)
        {
            await context.CommitAsync().ConfigureAwait(false);
            return;
        }

        var exception = message.Exceptions?.FirstOrDefault();
        process = process with
        {
            Status = BookPrintProcessStatus.Error,
            ErrorMessage = exception?.Message ?? "The print-completion notification was exhausted.",
        };
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = process.Id.ToString("D"),
            Operation = nameof(BookPrintProcessFailureHandler),
            Timestamp = _clock.GetCurrentInstant(),
        }).ConfigureAwait(false);
        await context.UpdateBookPrintProcessAsync(process).ConfigureAwait(false);
        await context.CommitAsync().ConfigureAwait(false);
    }
}
