// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated Minimal API route, query, body, and cancellation binding.</summary>
[TestClass]
public sealed class MinimalApiBindingTests
{
    /// <summary>Verifies route and query values override body values and server-set input.</summary>
    [TestMethod]
    public async Task BindsRouteQueryBodyAndServerSetValues()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/hosting/requests/42?Filter=query",
            new HostingRequest
            {
                Id = 999,
                Filter = "body",
                Value = "body-value",
                ServerStamp = "client-value",
            },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HostingResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Message.Should().Be("42:query:body-value");
        fixture.State.LastRequestServerStamp.Should().BeNull();
    }

    /// <summary>Verifies optional query binding and the generated cancellation token.</summary>
    [TestMethod]
    public async Task BindsOptionalQueryAndCancellationToken()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/hosting/queries/7"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HostingResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Message.Should().Be("7:");

        using var requestResponse = await client.PostAsJsonAsync(
            "/hosting/requests/8",
            new HostingRequest { Value = "cancelable" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        requestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.State.RequestCancellationTokenWasCancelable.Should().BeTrue();
    }

    /// <summary>Verifies invalid route and query values are rejected before handler execution.</summary>
    [TestMethod]
    public async Task RejectsInvalidRouteAndQueryBinding()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var routeResponse = await client.GetAsync(
            new Uri("http://localhost/hosting/queries/not-an-integer"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        using var queryResponse = await client.GetAsync(
            new Uri("http://localhost/hosting/stream?Count=not-an-integer"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        routeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        queryResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fixture.State.RequestExecutions.Should().Be(0);
    }

    /// <summary>Verifies malformed request bodies are rejected before dispatch.</summary>
    [TestMethod]
    public async Task RejectsInvalidBodyBinding()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/requests/1"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fixture.State.RequestExecutions.Should().Be(0);
    }

    /// <summary>Verifies versioned route binding exposes only the active contract versions.</summary>
    [TestMethod]
    public async Task BindsOnlyActiveVersionedRoutes()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var retiredResponse = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/versioned/5?Value=retired"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        using var activeResponse = await client.GetAsync(
            new Uri("http://localhost/api/v2/hosting/versioned/5?Value=active"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        using var secondActiveResponse = await client.GetAsync(
            new Uri("http://localhost/api/v3/hosting/versioned/5?Value=active-three"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        using var futureResponse = await client.GetAsync(
            new Uri("http://localhost/api/v4/hosting/versioned/5?Value=future"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        retiredResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        activeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondActiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        futureResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var result = await activeResponse.Content.ReadFromJsonAsync<HostingResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Message.Should().Be("5:active");
    }
}
