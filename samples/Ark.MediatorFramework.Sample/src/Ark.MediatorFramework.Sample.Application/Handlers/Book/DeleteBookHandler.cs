// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

using NodaTime;

using System.Security.Claims;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Deletes books through the application contract.</summary>
public sealed class DeleteBookHandler : IRequestHandler<Book_DeleteRequest.V1, bool>
{
    private readonly ISampleDataContextFactory _factory;
    private readonly IContextProvider<ClaimsPrincipal> _user;
    private readonly IClock _clock;

    /// <summary>Initializes a new instance of the <see cref="DeleteBookHandler"/> class.</summary>
    public DeleteBookHandler(ISampleDataContextFactory factory, IContextProvider<ClaimsPrincipal> user, IClock clock)
    {
        _factory = factory;
        _user = user;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteAsync(Book_DeleteRequest.V1 request, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        if (!await context.DeleteBookAsync(request.Id, ctk).ConfigureAwait(false))
            throw new EntityNotFoundException($"Book '{request.Id}' was not found.");
        await context.WriteAuditAsync(new AuditEntry
        {
            Id = Guid.NewGuid(),
            UserId = _user.GetUserId() ?? "anonymous",
            EntityType = nameof(Book.V1.Output),
            Identifier = request.Id.ToString("D"),
            Operation = typeof(Book_DeleteRequest).Name + "." + typeof(Book_DeleteRequest.V1).Name,
            Timestamp = _clock.GetCurrentInstant(),
        }, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return true;
    }
}
