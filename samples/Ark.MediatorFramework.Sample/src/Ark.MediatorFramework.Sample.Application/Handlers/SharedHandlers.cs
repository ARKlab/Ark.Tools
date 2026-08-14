// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;
using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Handles paged reads of the persisted audit trail.</summary>
public sealed class GetAuditsHandler : IQueryHandler<GetAuditsQuery, PagedResult<AuditRecord>>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetAuditsHandler"/> class.</summary>
    public GetAuditsHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditRecord>> ExecuteAsync(
        GetAuditsQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        await using var __ctx = context.ConfigureAwait(false);
        var result = await context.ReadAuditsAsync(query, ctk).ConfigureAwait(false);
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return result;
    }
}
