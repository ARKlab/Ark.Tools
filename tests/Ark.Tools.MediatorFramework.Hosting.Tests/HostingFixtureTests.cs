// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;
using Ark.Tools.Solid;

using AwesomeAssertions;

using SimpleInjector.Lifestyles;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Smoke tests for the framework-owned synthetic hosting fixture.</summary>
[TestClass]
public sealed class HostingFixtureTests
{
    /// <summary>Ensures all host layers build and the fixture disposes its resources.</summary>
    [TestMethod]
    public async Task FixtureBuildsHostsAndDisposesResources()
    {
        var fixture = new HostingTestFixture();
        try
        {
            var minimalApiHost = fixture.BuildMinimalApiHost();
            var grpcHost = fixture.BuildGrpcHost();
            var rebusHost = fixture.BuildRebusHost();

            await using (AsyncScopedLifestyle.BeginScope(fixture.Container).ConfigureAwait(false))
            {
                var handler = fixture.Container.GetInstance<IRequestHandler<HostingRequest, HostingResponse>>();
                var response = await handler.ExecuteAsync(new HostingRequest
                {
                    Id = 7,
                    Filter = "filter",
                    Value = "value",
                }, CancellationToken.None).ConfigureAwait(false);

                response.Message.Should().Be("7:filter:value");
                minimalApiHost.Should().NotBeNull();
                grpcHost.Should().NotBeNull();
                rebusHost.Should().NotBeNull();
            }
        }
        finally
        {
            await fixture.DisposeAsync().ConfigureAwait(false);
        }

        fixture.State.RequestExecutions.Should().Be(1);
        fixture.IsDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Ensures a Rebus command sent via the bus is routed to the correct input queue,
    /// consumed by the registered handler, and leaves no pending messages in the network.
    /// </summary>
    [TestMethod]
    public async Task RebusCommandIsDeliveredAndProcessed()
    {
        var fixture = new HostingTestFixture();
        await using (fixture.ConfigureAwait(false))
        {
            var bus = fixture.BuildRebusHost();

            await bus.Send(new HostingRebusCommand()).ConfigureAwait(false);

            await fixture.WaitForIdleAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            fixture.State.CommandExecutions.Should().Be(1);
        }
    }
}
