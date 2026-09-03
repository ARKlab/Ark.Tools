// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated Minimal API command status semantics.</summary>
[TestClass]
public sealed class MinimalApiCommandTests
{
    /// <summary>Verifies an HTTP-only command executes inline and returns 204 No Content.</summary>
    [TestMethod]
    public async Task ExecutesCommandInlineWithNoContent()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/commands",
            new HostingCommand { Value = "inline" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await fixture.State.CommandExecuted.WaitAsync(TimeSpan.FromSeconds(10), app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        fixture.State.CommandExecutions.Should().Be(1);
    }

    /// <summary>Verifies a null request handler result maps to the default 204 No Content.</summary>
    [TestMethod]
    public async Task MapsNullRequestResultToNoContent()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/no-content",
            new HostingNoContentRequest { Value = "empty" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Verifies a dual HTTP+Rebus command executes its handler inline and returns 204, ignoring `[RebusMessage]`.</summary>
    [TestMethod]
    public async Task ExecutesDualCommandInlineWithNoContent()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/hosting/bus-commands",
            new HostingBusCommand { Value = "inline" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        fixture.State.BusCommandExecutions.Should().Be(1);
        fixture.State.LastBusCommandValue.Should().Be("inline");
    }
}
