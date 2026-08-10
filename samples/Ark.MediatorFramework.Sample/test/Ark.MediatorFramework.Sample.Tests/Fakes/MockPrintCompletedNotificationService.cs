// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;

using Moq;

namespace Ark.MediatorFramework.Sample.Tests.Fakes;

/// <summary>Controls deterministic failures from the simulated external print-completion service.</summary>
internal sealed class MockPrintCompletedNotificationService
{
    private int _pendingFailures;

    /// <summary>Initializes a strict mock with a notification setup.</summary>
    public MockPrintCompletedNotificationService()
    {
        Mock = new Mock<IPrintCompletedNotificationService>(MockBehavior.Strict);
        Mock
            .Setup(service => service.NotifyAsync(
                It.IsAny<BookPrintProcessResponse>(),
                It.IsAny<CancellationToken>()))
            .Returns((BookPrintProcessResponse process, CancellationToken ctk) => NotifyAsync(process, ctk));
    }

    /// <summary>Gets the configured external-service mock.</summary>
    public Mock<IPrintCompletedNotificationService> Mock { get; }

    /// <summary>Configures the number of subsequent notifications that fail.</summary>
    /// <param name="count">The number of failures to simulate.</param>
    public void FailNext(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Volatile.Write(ref _pendingFailures, count);
    }

    /// <summary>Verifies a notification was sent for the supplied process.</summary>
    /// <param name="process">The expected process.</param>
    public void VerifyNotification(BookPrintProcessResponse process)
    {
        ArgumentNullException.ThrowIfNull(process);
        Mock.Verify(service => service.NotifyAsync(
            It.Is<BookPrintProcessResponse>(candidate => candidate.Id == process.Id),
            It.IsAny<CancellationToken>()));
    }

    private async Task NotifyAsync(BookPrintProcessResponse process, CancellationToken ctk)
    {
        ArgumentNullException.ThrowIfNull(process);
        ctk.ThrowIfCancellationRequested();
        if (Interlocked.Decrement(ref _pendingFailures) >= 0)
            throw new InvalidOperationException("The simulated print-completion service failed.");

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
