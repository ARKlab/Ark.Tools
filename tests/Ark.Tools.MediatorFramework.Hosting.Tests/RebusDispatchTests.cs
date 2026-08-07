// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using System.Security.Claims;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Verifies generated Rebus dispatch through a real in-memory processor.</summary>
[TestClass]
public sealed class RebusDispatchTests
{
    /// <summary>Ensures generated dispatch creates scopes, flows the user, and supplies cancellation.</summary>
    [TestMethod]
    public async Task GeneratedWrapperDispatchesWithScopeAndUserContext()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            fixture.PrincipalProvider.SetCurrent(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "hosting-user")],
                "test")));
            var bus = fixture.BuildRebusHost();

            await bus.Send(new HostingRebusCommand()).ConfigureAwait(false);
            await bus.Send(new HostingRebusCommand()).ConfigureAwait(false);
            await fixture.State.CommandExecuted.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            fixture.State.CommandExecutions.Should().Be(2);
            fixture.State.RebusScopeIds.Should().HaveCount(2);
            fixture.State.RebusScopeIds.Distinct().Should().HaveCount(2);
            fixture.State.RebusUserId.Should().Be("hosting-user");
            fixture.State.RebusCancellationTokenWasCancelable.Should().BeTrue();
        }
    }

    /// <summary>Ensures deferred messages are visible to bounded idle diagnostics.</summary>
    [TestMethod]
    public async Task DeferredMessageIsTrackedAndCanBeIgnoredExplicitly()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            var bus = fixture.BuildRebusHost();
            await bus.Send(new HostingDeferredCommand()).ConfigureAwait(false);
            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5), ignoreDeferred: true).ConfigureAwait(false);

            fixture.GetRebusCounts().Deferred.Should().Be(1);
            fixture.State.DeferredMessages.Should().Be(1);
        }
    }

    /// <summary>Ensures generated dispatch supplies the processor cancellation token.</summary>
    [TestMethod]
    public async Task GeneratedWrapperSuppliesCancellationToken()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            var bus = fixture.BuildRebusHost();
            await bus.Send(new HostingCancellationCommand()).ConfigureAwait(false);
            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            fixture.State.RebusCancellationTokenWasCancelable.Should().BeTrue();
        }
    }
}
