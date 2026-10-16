// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;
using Ark.Tools.MediatorFramework.MinimalApi;

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
            "/api/v1/hosting/requests/11?Filter=json")
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
            "/api/v1/hosting/requests/12?Filter=packed")
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

    /// <summary>Verifies Accept header parsing for MessagePack negotiation.</summary>
    /// <param name="accept">The Accept header value.</param>
    /// <param name="expected">Whether MessagePack should be preferred.</param>
    [TestMethod]
    [DataRow(null, false)]
    [DataRow("", false)]
    [DataRow("application/json", false)]
    [DataRow("application/x-msgpack", true)]
    [DataRow("APPLICATION/X-MSGPACK", true)]
    [DataRow("application/json, application/x-msgpack", true)]
    [DataRow(" application/x-msgpack ; charset=utf-8 ", true)]
    [DataRow("application/x-msgpack;q=1", true)]
    [DataRow("application/x-msgpack; q=0", false)]
    [DataRow("application/x-msgpack;level=1; Q=0", false)]
    [DataRow("application/x-msgpack;q=0, application/x-msgpack", true)]
    [DataRow("application/x-msgpack-ext", false)]
    [DataRow("text/plain;p=application/x-msgpack", false)]
    [DataRow(",,application/x-msgpack,", true)]
    [DataRow("application/x-msgpack;q=0.5, application/json;q=0.4", true)]
    [DataRow("application/x-msgpack;q=0.5", true)]
    [DataRow("application/x-msgpack;q=0.001", true)]
    [DataRow("application/x-msgpack;q=0.000", false)]
    [DataRow("application/x-msgpack;q=0.", false)]
    [DataRow("application/x-msgpack;q=", true)]
    [DataRow("application/x-msgpack;q=abc", true)]
    [DataRow("application/x-msgpack;q=0x", true)]
    [DataRow("application/x-msgpack;q=-0", true)]
    public void ParsesAcceptHeader(string? accept, bool expected)
    {
        ArkMessagePackEx.PrefersMessagePackForGeneratedEndpoint(accept).Should().Be(expected);
    }
}
