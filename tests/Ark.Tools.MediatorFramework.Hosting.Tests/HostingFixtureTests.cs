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
        using (AsyncScopedLifestyle.BeginScope(fixture.Container))
        {
            var handler = fixture.Container.GetInstance<IRequestHandler<HostingRequest, HostingResponse>>();
            var response = await handler.ExecuteAsync(new HostingRequest
            {
                Id = 7,
                Filter = "filter",
                Value = "value",
            }).ConfigureAwait(false);

            response.Message.Should().Be("7:filter:value");
            fixture.BuildMinimalApiHost().Should().NotBeNull();
            fixture.BuildGrpcHost().Should().NotBeNull();
            fixture.BuildRebusHost().Should().NotBeNull();
        }

        await fixture.DisposeAsync().ConfigureAwait(false);

        fixture.State.RequestExecutions.Should().Be(1);
        fixture.IsDisposed.Should().BeTrue();
    }
}
