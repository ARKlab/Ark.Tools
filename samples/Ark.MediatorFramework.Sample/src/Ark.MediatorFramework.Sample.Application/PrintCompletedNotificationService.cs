// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Sample.Application;

/// <summary>Notifies an external service that a book print process completed.</summary>
public interface IPrintCompletedNotificationService
{
    /// <summary>Sends the completion notification.</summary>
    /// <param name="process">The completed print process.</param>
    /// <param name="ctk">The cancellation token.</param>
    Task NotifyAsync(BookPrintProcessResponse process, CancellationToken ctk = default);
}

/// <summary>Default no-op implementation of the external print-completion notification service.</summary>
public sealed class NoOpPrintCompletedNotificationService : IPrintCompletedNotificationService
{
    /// <inheritdoc />
    public async Task NotifyAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
