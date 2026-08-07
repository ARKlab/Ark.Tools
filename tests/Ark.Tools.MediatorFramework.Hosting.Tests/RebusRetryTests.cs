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
}
