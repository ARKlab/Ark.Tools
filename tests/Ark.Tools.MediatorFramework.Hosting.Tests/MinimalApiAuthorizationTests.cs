// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves authentication and transport-agnostic policy authorization.</summary>
[TestClass]
public sealed class MinimalApiAuthorizationTests
{
    /// <summary>Verifies an anonymous caller is challenged by the generated endpoint metadata.</summary>
    [TestMethod]
    public async Task RejectsAnonymousPrincipal()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/authorized"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        fixture.State.AuthorizedExecutions.Should().Be(0);
    }

    /// <summary>Verifies an authenticated caller without the required policy is forbidden.</summary>
    [TestMethod]
    public async Task RejectsAuthenticatedPrincipalWithoutPolicy()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "authenticated");

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/authorized"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        fixture.State.AuthorizedExecutions.Should().Be(0);
    }

    /// <summary>Verifies a caller with the required policy reaches the handler.</summary>
    [TestMethod]
    public async Task AllowsPrincipalWithRequiredPolicy()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "scope");

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/authorized"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<HostingResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Message.Should().Be("authorized");
        fixture.State.AuthorizedExecutions.Should().Be(1);
    }
}
