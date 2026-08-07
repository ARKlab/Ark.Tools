// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Verifies bounded retry and error queue behavior for generated Rebus wrappers.</summary>
[TestClass]
public sealed class RebusRetryTests
{
    /// <summary>Ensures a failing generated handler exhausts retries and reaches the error queue.</summary>
    [TestMethod]
    public async Task FailedMessageIsRetriedAndMovedToErrorQueue()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            var bus = fixture.BuildRebusHost();
            await bus.Send(new HostingRetryCommand()).ConfigureAwait(false);
            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            fixture.State.RetryAttempts.Should().Be(2);
            fixture.GetRebusCounts().Error.Should().Be(1);
        }
    }

    /// <summary>Ensures second-level retries dispatch the failed message to its handler.</summary>
    [TestMethod]
    public async Task SecondLevelRetryDispatchesFailedMessageToHandler()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            var bus = fixture.BuildRebusHost(secondLevelRetriesEnabled: true);
            await bus.Send(new HostingSecondLevelRetryCommand()).ConfigureAwait(false);
            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            fixture.State.SecondLevelRetryAttempts.Should().Be(2);
            fixture.State.FailedMessageExecutions.Should().Be(1);
            fixture.State.FailedMessageException.Should().Contain("Synthetic second-level retry failure.");
            fixture.GetRebusCounts().Error.Should().Be(0);
        }
    }

    /// <summary>Ensures a failed second-level handler moves the message to the error queue.</summary>
    [TestMethod]
    public async Task FailedSecondLevelRetryIsMovedToErrorQueue()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            fixture.State.FailSecondLevelRetryHandler = true;
            var bus = fixture.BuildRebusHost(secondLevelRetriesEnabled: true);
            await bus.Send(new HostingSecondLevelRetryCommand()).ConfigureAwait(false);
            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            fixture.State.SecondLevelRetryAttempts.Should().Be(2);
            fixture.State.FailedMessageExecutions.Should().Be(1);
            fixture.GetRebusCounts().Error.Should().Be(1);
        }
    }
}
