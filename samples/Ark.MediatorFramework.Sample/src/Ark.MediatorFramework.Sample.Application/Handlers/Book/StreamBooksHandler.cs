// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

namespace Ark.MediatorFramework.Sample.Application.Handlers;

/// <summary>Produces Book items incrementally for HTTP JSON and gRPC streaming.</summary>
public sealed class StreamBooksHandler : IQueryHandler<StreamBooksQuery.V1, IAsyncEnumerable<BookStreamItem>>
{
    private const int _maximumCount = 100;
    private const int _maximumDelayMilliseconds = 10_000;

    /// <inheritdoc />
    public async Task<IAsyncEnumerable<BookStreamItem>> ExecuteAsync(
        StreamBooksQuery.V1 query,
        CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Count is < 0 or > _maximumCount)
            throw new ArgumentOutOfRangeException(nameof(query), query.Count, $"Count must be between 0 and {_maximumCount}.");
        if (query.DelayMilliseconds is < 0 or > _maximumDelayMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(query), query.DelayMilliseconds, $"DelayMilliseconds must be between 0 and {_maximumDelayMilliseconds}.");

        await Task.CompletedTask.ConfigureAwait(false);
        return _streamAsync(query, ctk);
    }

    private static async IAsyncEnumerable<BookStreamItem> _streamAsync(
        StreamBooksQuery.V1 query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ctk)
    {
        for (var index = 0; index < query.Count; index++)
        {
            ctk.ThrowIfCancellationRequested();
            yield return new BookStreamItem
            {
                Index = index,
                Title = $"Book {index}",
                Author = $"Author {index}",
            };

            if (index + 1 < query.Count)
                await Task.Delay(query.DelayMilliseconds, ctk).ConfigureAwait(false);
        }
    }
}
