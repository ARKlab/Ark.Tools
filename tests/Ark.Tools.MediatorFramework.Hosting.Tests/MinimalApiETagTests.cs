// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated ETag precondition binding and response emission over HTTP.</summary>
[TestClass]
public sealed class MinimalApiETagTests
{
    /// <summary>Verifies an If-Match header overrides the ETag token supplied in the body.</summary>
    [TestMethod]
    public async Task IfMatchHeaderOverridesBodyToken()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/hosting/etag/1")
        {
            Content = JsonContent.Create(new HostingETagUpdateRequest { Value = "next", ETag = "body-token" }),
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"header-token\""));

        using var response = await client.SendAsync(
            request, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.State.LastETagReceived.Should().Be("header-token");
        var result = await response.Content.ReadFromJsonAsync<HostingETagResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result!.ReceivedETag.Should().Be("header-token");
    }

    /// <summary>Verifies the body ETag token is used when no precondition header is supplied.</summary>
    [TestMethod]
    public async Task BodyTokenIsUsedWithoutPreconditionHeaders()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/v1/hosting/etag/1",
            new HostingETagUpdateRequest { Value = "next", ETag = "body-token" },
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.State.LastETagReceived.Should().Be("body-token");
    }

    /// <summary>Verifies the response emits a quoted ETag header while keeping the token in the body.</summary>
    [TestMethod]
    public async Task EmitsETagHeaderAndKeepsTokenInBody()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/etag/1"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.Should().Be("\"hosting-v2\"");
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var json = JsonNode.Parse(body)!.AsObject();
        json["token"]?.GetValue<string>().Should().Be("hosting-v2");
    }

    /// <summary>Verifies a matching If-None-Match on a GET short-circuits to 304 Not Modified.</summary>
    [TestMethod]
    public async Task ConditionalGetReturnsNotModifiedOnMatch()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hosting/etag/1");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"hosting-v2\""));

        using var response = await client.SendAsync(
            request, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NotModified);
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        body.Should().BeEmpty();
    }
}
