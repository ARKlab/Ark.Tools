// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.GrpcClient;
using Ark.MediatorFramework.Sample.Tests.Auth;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.MediatorFramework.Sample.Application;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using GrpcGetGreetingsStreamQuery = Ark.MediatorFramework.Sample.GrpcClient.GetGreetingsStreamQuery;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies incremental delivery of async-enumerable responses.</summary>
[TestClass]
public sealed class AsyncEnumerableStreamingTests
{
    /// <summary>Reads the first JSON object before a delayed producer can finish.</summary>
    [TestMethod]
    public async Task HttpStreamDeliversFirstItemBeforeProducerCompletes()
    {
        using var context = new SampleTestContext();
        AddAuthorization(context.Client);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/greetings/stream?count=2&delayMilliseconds=1500");
        var stopwatch = Stopwatch.StartNew();

        using var response = await context.Client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var firstItem = await ReadFirstJsonObjectAsync(stream).ConfigureAwait(false);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(1200));
        firstItem.GetProperty("index").GetInt32().Should().Be(0);
        firstItem.GetProperty("message").GetString().Should().Be("Hello, stream item 0!");
    }

    /// <summary>Returns a plain JSON array rather than server-sent-event frames.</summary>
    [TestMethod]
    public async Task HttpStreamUsesPlainJsonArray()
    {
        using var context = new SampleTestContext();
        AddAuthorization(context.Client);

        using var response = await context.Client.GetAsync(
            "/api/v1/greetings/stream?count=2&delayMilliseconds=0").ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        body.Should().NotContain("data:");
        using var document = JsonDocument.Parse(body);
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        document.RootElement.GetArrayLength().Should().Be(2);
    }

    /// <summary>Consumes gRPC items incrementally and cancels the server stream.</summary>
    [TestMethod]
    public async Task GrpcStreamDeliversIncrementallyAndSupportsCancellation()
    {
        using var context = new SampleTestContext();
        var token = AddAuthorization(context.Client);
        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = context.CreateGrpcHandler() });
        var client = new GreetingsV1.GreetingsV1Client(channel);
        using var cancellation = new CancellationTokenSource();
        using var call = client.GetGreetingsStream(
            new GrpcGetGreetingsStreamQuery { Count = 100, DelayMilliseconds = 1500 },
            new Metadata { { "authorization", "Bearer " + token } });

        var stopwatch = Stopwatch.StartNew();
        (await call.ResponseStream.MoveNext(cancellation.Token).ConfigureAwait(false)).Should().BeTrue();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(1200));
        call.ResponseStream.Current.Index.Should().Be(0);
        cancellation.Cancel();

        var exception = await Assert.ThrowsExceptionAsync<RpcException>(
            async () => await call.ResponseStream.MoveNext(cancellation.Token).ConfigureAwait(false));
        exception.StatusCode.Should().Be(StatusCode.Cancelled);
    }

    /// <summary>Returns an empty JSON array and an empty gRPC stream for zero items.</summary>
    [TestMethod]
    public async Task EmptyStreamIsEmptyOnHttpAndGrpc()
    {
        using var context = new SampleTestContext();
        var token = AddAuthorization(context.Client);
        using var response = await context.Client.GetAsync(
            "/api/v1/greetings/stream?count=0&delayMilliseconds=0").ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Be("[]");

        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = context.CreateGrpcHandler() });
        using var call = new GreetingsV1.GreetingsV1Client(channel).GetGreetingsStream(
            new GrpcGetGreetingsStreamQuery { Count = 0 },
            new Metadata { { "authorization", "Bearer " + token } });

        (await call.ResponseStream.MoveNext().ConfigureAwait(false)).Should().BeFalse();
    }

    private static string AddAuthorization(HttpClient client)
    {
        var token = new JwtTokenBuilder()
            .AddSubject("stream-test-user")
            .AddScope(ApplicationScopes.GreetingWrite)
            .Build();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }

    private static async Task<JsonElement> ReadFirstJsonObjectAsync(Stream stream)
    {
        var bytes = new List<byte>();
        var buffer = new byte[1];
        var started = false;
        var depth = 0;
        while (await stream.ReadAsync(buffer).ConfigureAwait(false) > 0)
        {
            var value = buffer[0];
            if (value == (byte)'{')
            {
                started = true;
                depth++;
            }
            else if (started && value == (byte)'}')
                depth--;

            if (started)
                bytes.Add(value);
            if (started && depth == 0)
                break;
        }

        using var document = JsonDocument.Parse(bytes.ToArray());
        return document.RootElement.Clone();
    }
}
