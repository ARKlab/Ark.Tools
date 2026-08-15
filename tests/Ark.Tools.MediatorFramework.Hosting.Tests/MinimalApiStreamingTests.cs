// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Json;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated asynchronous streaming and cancellation behavior.</summary>
[TestClass]
public sealed class MinimalApiStreamingTests
{
    /// <summary>Verifies a stream is serialized as a JSON array.</summary>
    [TestMethod]
    public async Task StreamsPlainJsonArray()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/stream?Count=3"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var items = await response.Content.ReadFromJsonAsync<HostingStreamItem[]>(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        items.Should().NotBeNull();
        items!.Select(item => item.Number).Should().Equal(1, 2, 3);
    }

    /// <summary>Verifies the first item is readable before the producer is released.</summary>
    [TestMethod]
    public async Task StreamsFirstItemBeforeProducerCompletes()
    {
        await using var fixture = new HostingTestFixture();
        fixture.State.HoldStreamAfterFirst = true;
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/stream?Count=2"),
            HttpCompletionOption.ResponseHeadersRead,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var buffer = new char[64];
        var output = new StringBuilder();
        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (!output.ToString().Contains("\"number\":1", StringComparison.Ordinal))
            {
                var read = await reader.ReadAsync(buffer, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
                read.Should().BeGreaterThan(0);
                output.Append(buffer, 0, read);
            }

            fixture.State.StreamFirstItemProduced.IsCompleted.Should().BeTrue();
            fixture.State.StreamCancellationObserved.IsCompleted.Should().BeFalse();
            fixture.State._releaseStream();
            output.Append(await reader.ReadToEndAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false));
        }

        output.ToString().Should().Contain("\"number\":2");
    }

    /// <summary>Verifies an empty asynchronous sequence produces an empty JSON array.</summary>
    [TestMethod]
    public async Task StreamsEmptySequence()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/api/v1/hosting/stream?Count=0"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false))
            .Should().Be("[]");
    }

    /// <summary>Verifies request cancellation reaches the asynchronous producer.</summary>
    [TestMethod]
    public async Task ObservesRequestCancellationInProducer()
    {
        await using var fixture = new HostingTestFixture();
        fixture.State.HoldStreamAfterFirst = true;
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var cts = new CancellationTokenSource();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hosting/stream?Count=2");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var buffer = new char[64];
            var output = new StringBuilder();
            while (!output.ToString().Contains("\"number\":1", StringComparison.Ordinal))
            {
                var read = await reader.ReadAsync(buffer, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
                read.Should().BeGreaterThan(0);
                output.Append(buffer, 0, read);
            }

            fixture.State.StreamFirstItemProduced.IsCompleted.Should().BeTrue();
            await cts.CancelAsync().ConfigureAwait(false);
            response.Dispose();

            await fixture.State.StreamCancellationObserved.WaitAsync(
                TimeSpan.FromSeconds(5),
                app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        }
    }
}
