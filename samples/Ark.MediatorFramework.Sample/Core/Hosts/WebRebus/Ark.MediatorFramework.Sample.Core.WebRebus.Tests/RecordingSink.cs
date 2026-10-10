// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Core.WebRebus.Tests;

/// <summary>Records the books a subscriber was notified about.</summary>
internal sealed class RecordingSink : IBookPrintNotificationSink, IBookPrintAuditSink
{
    private readonly ConcurrentQueue<BookId> _bookIds = new();

    /// <summary>Gets the number of recorded books.</summary>
    public int Count => _bookIds.Count;

    /// <summary>Gets the recorded book identifiers in arrival order.</summary>
    public IReadOnlyCollection<BookId> BookIds => _bookIds;

    /// <inheritdoc />
    public async Task RecordAsync(BookId bookId, CancellationToken ctk = default)
    {
        _bookIds.Enqueue(bookId);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
