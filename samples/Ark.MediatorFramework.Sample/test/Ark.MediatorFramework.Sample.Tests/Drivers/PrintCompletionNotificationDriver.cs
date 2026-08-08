// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using Reqnroll;

namespace Ark.MediatorFramework.Sample.Tests.Drivers;

/// <summary>Controls the simulated external print-completion service for a scenario.</summary>
[Binding]
public sealed class PrintCompletionNotificationDriver
{
    private readonly SampleTestContext _sampleContext;

    /// <summary>Initializes a new instance of the <see cref="PrintCompletionNotificationDriver"/> class.</summary>
    /// <param name="sampleContext">The scenario-owned application context.</param>
    public PrintCompletionNotificationDriver(SampleTestContext sampleContext)
    {
        _sampleContext = sampleContext;
    }

    /// <summary>Configures every Rebus delivery attempt to fail when notifying the external service.</summary>
    [Given("the print-completion notification service fails")]
    public void PrintCompletionNotificationServiceFails()
    {
        _sampleContext.Application.FailNextPrintCompletionNotifications(int.MaxValue);
    }
}
