// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Text.Json.Nodes;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated OpenAPI documents and schema conventions.</summary>
[TestClass]
public sealed class MinimalApiOpenApiTests
{
    /// <summary>Verifies v1 paths, documentation, schemas, and server-set filtering.</summary>
    [TestMethod]
    public async Task V1DocumentContainsFilteredSchemasAndDocumentation()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/openapi/v1.json"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var document = JsonNode.Parse(body)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        document["openapi"]?.GetValue<string>().Should().Be("3.1.1");

        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("The OpenAPI document had no paths.");
        paths["/api/v1/hosting/requests/{id}"]?["post"].Should().NotBeNull();
        paths["/api/v1/hosting/versioned/{id}"].Should().BeNull();
        paths["/api/v2/hosting/versioned/{id}"].Should().BeNull();

        paths["/api/v1/hosting/openapi"]?["get"]?["summary"]?.GetValue<string>()
            .Should().Be("Query used to expose the generated OpenAPI response schema.");

        var responseSchema = document["components"]?["schemas"]?["HostingOpenApiResponse"]
            ?? throw new InvalidOperationException("The OpenAPI response schema was not generated.");
        responseSchema["properties"]?["serverStamp"].Should().BeNull();
        responseSchema["properties"]?["date"]?["format"]?.GetValue<string>().Should().Be("date");
        responseSchema["properties"]?["shape"]?["oneOf"]?.AsArray().Count.Should().Be(1);
    }

    /// <summary>Verifies v2 contains active v2 operations without other document versions.</summary>
    [TestMethod]
    public async Task V2DocumentContainsOnlyV2Operations()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/openapi/v2.json"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var document = JsonNode.Parse(body)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        var paths = document["paths"]?.AsObject()
            ?? throw new InvalidOperationException("The OpenAPI document had no paths.");

        paths["/api/v2/hosting/requests/{id}"]?["post"].Should().NotBeNull();
        paths["/api/v2/hosting/versioned/{id}"]?["get"].Should().NotBeNull();
        paths["/api/v1/hosting/requests/{id}"].Should().BeNull();
        paths["/api/v3/hosting/versioned/{id}"].Should().BeNull();
    }
}
