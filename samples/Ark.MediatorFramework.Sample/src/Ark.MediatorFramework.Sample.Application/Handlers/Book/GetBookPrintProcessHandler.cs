// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;
using Ark.Tools.Core;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Reads a book print process.</summary>
public sealed class GetBookPrintProcessHandler :
    IQueryHandler<GetBookPrintProcessQuery, BookPrintProcessResponse>
{
    private readonly ISampleDataContextFactory _factory;

    /// <summary>Initializes a new instance of the <see cref="GetBookPrintProcessHandler"/> class.</summary>
    public GetBookPrintProcessHandler(ISampleDataContextFactory factory)
    {
        _factory = factory;
    }

    /// <inheritdoc />
    public async Task<BookPrintProcessResponse> ExecuteAsync(
        GetBookPrintProcessQuery query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var context = await _factory.CreateAsync(ctk).ConfigureAwait(false);
        var process = await context.ReadBookPrintProcessAsync(query.Id, ctk).ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Book print process '{query.Id}' was not found.");
        await context.CommitAsync(ctk).ConfigureAwait(false);
        return process;
    }
}
