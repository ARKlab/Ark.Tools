// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using System.Net;
using System.Text.Json.Nodes;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies strict forwarded-prefix handling in the sample host.</summary>
[TestClass]
public sealed class ForwardedPrefixTests
{
    /// <summary>Valid prefixes are applied to the request path base.</summary>
    [TestMethod]
    public async Task ValidPrefixIsAccepted()
    {
        using var context = new TransportTestContext();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthCheck");
        request.Headers.Add("X-Forwarded-Prefix", "/gateway");

        var response = await context.Client.SendAsync(request).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Malformed prefixes are rejected before endpoint execution.</summary>
    [TestMethod]
    public async Task InvalidPrefixIsRejected()
    {
        using var context = new TransportTestContext();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthCheck");
        request.Headers.Add("X-Forwarded-Prefix", "/gateway/../internal");

        var response = await context.Client.SendAsync(request).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Multiple forwarded prefixes are rejected as ambiguous.</summary>
    [TestMethod]
    public async Task MultiplePrefixesAreRejected()
    {
        using var context = new TransportTestContext();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthCheck");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Prefix", ["/one", "/two"]);

        var response = await context.Client.SendAsync(request).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Valid prefixes preserve generated OpenAPI routing and document paths.</summary>
    [TestMethod]
    public async Task ValidPrefixPreservesGeneratedOpenApiRouting()
    {
        using var context = new TransportTestContext();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("X-Forwarded-Prefix", "/gateway");

        using var response = await context.Client.SendAsync(request).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var document = JsonNode.Parse(responseBody)
            ?? throw new InvalidOperationException("The OpenAPI document was not valid JSON.");
        document["paths"]?["/api/v1/greetings/{id}"]?["get"].Should().NotBeNull();
    }
}
