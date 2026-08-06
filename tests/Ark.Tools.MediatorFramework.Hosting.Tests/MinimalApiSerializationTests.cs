// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using MessagePack;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated JSON and MessagePack negotiation.</summary>
[TestClass]
public sealed class MinimalApiSerializationTests
{
    /// <summary>Verifies JSON request and response negotiation.</summary>
    [TestMethod]
    public async Task NegotiatesJsonRequestAndResponse()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/hosting/requests/11?Filter=json")
        {
            Content = new StringContent(
                """{"value":"json"}""",
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(
            request,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        var result = await response.Content.ReadFromJsonAsync<HostingResponse>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        result.Should().NotBeNull();
        result!.Message.Should().Be("11:json:json");
    }

    /// <summary>Verifies MessagePack request and response negotiation.</summary>
    [TestMethod]
    public async Task NegotiatesMessagePackRequestAndResponse()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        var requestBytes = MessagePackSerializer.Serialize(
            new HostingRequest { Value = "message-pack" },
            MessagePackSerializerOptions.Standard,
            app.Lifetime.ApplicationStopping);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/hosting/requests/12?Filter=packed")
        {
            Content = new ByteArrayContent(requestBytes),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-msgpack");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-msgpack"));

        using var response = await client.SendAsync(
            request,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/x-msgpack");
        var responseBytes = await response.Content.ReadAsByteArrayAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var result = MessagePackSerializer.Deserialize<HostingResponse>(
            responseBytes,
            MessagePackSerializerOptions.Standard,
            app.Lifetime.ApplicationStopping);
        result.Should().NotBeNull();
        result!.Message.Should().Be("12:packed:message-pack");
    }
}
