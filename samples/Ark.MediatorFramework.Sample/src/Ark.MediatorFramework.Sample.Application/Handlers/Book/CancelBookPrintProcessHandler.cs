// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Core.BusinessRuleViolation;
using Ark.Tools.Solid;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Cancels a pending or running book print process.</summary>
public sealed class CancelBookPrintProcessHandler :
    IRequestHandler<CancelBookPrintProcessRequest, BookPrintProcessResponse>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="CancelBookPrintProcessHandler"/> class.</summary>
    public CancelBookPrintProcessHandler(
        ISampleDataContextFactory factory,
        IContextProvider<ClaimsPrincipal> user,
        IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        CancelBookPrintProcessRequest request,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var cancelled = await context.CancelBookPrintProcessAsync(request.Id, ctk).ConfigureAwait(false);
        if (cancelled is null)
        {
            var current = await context.ReadBookPrintProcessAsync(request.Id, forUpdate: true, ctk: ctk).ConfigureAwait(false)
                ?? throw new EntityNotFoundException($"Book print process '{request.Id}' was not found.");
            throw new BusinessRuleViolationException(
                new BookPrintProcessCannotBeCancelledViolation(request.Id, current.Status));
        }
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(BookPrintProcessResponse),
            Identifier = request.Id.ToString("D"),
            Operation = nameof(CancelBookPrintProcessRequest),
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return cancelled;
    }
}
