// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Concurrent;

namespace Ark.MediatorFramework.Sample.Core.Tests.Fakes;

/// <summary>Records the books a subscriber was notified about.</summary>
public sealed class RecordingBookPrintSink : IBookPrintNotificationSink, IBookPrintAuditSink
{
    private readonly ConcurrentQueue<Guid> _bookIds = new();

    /// <summary>Gets the recorded book identifiers in arrival order.</summary>
    public IReadOnlyCollection<Guid> BookIds => _bookIds;

    /// <inheritdoc />
    public async Task RecordAsync(Guid bookId, CancellationToken ctk = default)
    {
        _bookIds.Enqueue(bookId);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
