// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

namespace Ark.MediatorFramework.Sample.Tests.Fakes;

/// <summary>Controls deterministic failures from the simulated external print-completion service.</summary>
public sealed class MockPrintCompletedNotificationService : IPrintCompletedNotificationService
{
    private int _pendingFailures;

    /// <summary>Configures the number of subsequent notifications that fail.</summary>
    /// <param name="count">The number of failures to simulate.</param>
    public void FailNext(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Volatile.Write(ref _pendingFailures, count);
    }

    /// <inheritdoc />
    public async Task NotifyAsync(BookPrintProcessResponse process, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(process);
        ctk.ThrowIfCancellationRequested();
        if (Interlocked.Decrement(ref _pendingFailures) >= 0)
            throw new InvalidOperationException("The simulated print-completion service failed.");

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
