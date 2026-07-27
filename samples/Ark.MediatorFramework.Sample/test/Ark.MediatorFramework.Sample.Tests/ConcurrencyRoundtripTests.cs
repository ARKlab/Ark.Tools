// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Auth;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies optimistic-concurrency round trips over HTTP and gRPC.</summary>
[TestClass]
public sealed class ConcurrencyRoundtripTests
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions().ConfigureArkDefaults();
    /// <summary>Creates, reads, updates, rejects a stale token, then retries over Minimal API.</summary>
    [TestMethod]
    public async Task MinimalApiRoundtripUsesAndRejectsStaleETags()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtTokenBuilder().AddSubject("test-user").AddScope("greetings.write").Build());

        var create = await context.Client.PostAsJsonAsync(
            "/api/v1/greetings",
            new { name = "etag-http" }).ConfigureAwait(false);
        var created = await create.Content.ReadFromJsonAsync<GreetingResponse>(JsonOptions).ConfigureAwait(false);
        created.Should().NotBeNull();

        var readResponse = await context.Client.GetAsync(
            new Uri($"/api/v1/greetings/{created!.Id}", UriKind.Relative)).ConfigureAwait(false);
        readResponse.Headers.ETag.Should().NotBeNull();
        var read = await readResponse.Content.ReadFromJsonAsync<GreetingResponse>(JsonOptions).ConfigureAwait(false);
        read.Should().NotBeNull();
        var originalETag = read!.ETag;
        originalETag.Should().NotBeNullOrWhiteSpace();

        using var conditional = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/greetings/{read.Id}");
        conditional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{originalETag}\"", isWeak: true));
        (await context.Client.SendAsync(conditional).ConfigureAwait(false)).StatusCode
            .Should().Be(HttpStatusCode.NotModified);

        using var first = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{read.Id}")
        {
            Content = JsonContent.Create(new { id = read.Id, message = "updated once", etag = originalETag }),
        };
        first.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{originalETag}\""));
        var firstResponse = await context.Client.SendAsync(first).ConfigureAwait(false);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await firstResponse.Content.ReadFromJsonAsync<GreetingResponse>(JsonOptions).ConfigureAwait(false);
        updated!.ETag.Should().NotBe(originalETag);

        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{read.Id}")
        {
            Content = JsonContent.Create(new { id = read.Id, message = "stale", etag = originalETag }),
        };
        stale.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{originalETag}\""));
        (await context.Client.SendAsync(stale).ConfigureAwait(false)).StatusCode
            .Should().Be(HttpStatusCode.PreconditionFailed);

        var reread = await context.Client.GetFromJsonAsync<GreetingResponse>(
            $"/api/v1/greetings/{read.Id}", JsonOptions).ConfigureAwait(false);
        using var retry = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{read.Id}")
        {
            Content = JsonContent.Create(new { id = read.Id, message = "updated twice", etag = reread!.ETag }),
        };
        retry.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{reread.ETag}\""));
        (await context.Client.SendAsync(retry).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Repeats the concurrency round trip using the generated gRPC client.</summary>
    [TestMethod]
    public async Task GrpcRoundtripUsesAndRejectsStaleETags()
    {
        using var context = new SampleTestContext();
        using var channel = GrpcChannel.ForAddress(
            "http://localhost", new GrpcChannelOptions { HttpHandler = context.CreateGrpcHandler() });
        var metadata = new Metadata
        {
            { "Authorization", "Bearer " + new JwtTokenBuilder().AddSubject("grpc-user").AddScope("greetings.write").Build() },
        };
        var client = new Ark.MediatorFramework.Sample.GrpcClient.GreetingsV1.GreetingsV1Client(channel);

        var created = await client.CreateGreetingAsync(
            new Ark.MediatorFramework.Sample.GrpcClient.CreateGreetingRequest { Name = "etag-grpc" }, metadata).ResponseAsync.ConfigureAwait(false);
        var read = await client.GetGreetingAsync(
            new Ark.MediatorFramework.Sample.GrpcClient.GetGreetingQuery { Id = created.Id }, metadata).ResponseAsync.ConfigureAwait(false);
        var originalETag = read.ETag;

        var updated = await client.UpdateGreetingMessageAsync(
            new Ark.MediatorFramework.Sample.GrpcClient.UpdateGreetingMessageRequest
            {
                Id = read.Id,
                Message = "updated once",
                ETag = originalETag,
            }, metadata).ResponseAsync.ConfigureAwait(false);
        updated.ETag.Should().NotBe(originalETag);

        var stale = async () => await client.UpdateGreetingMessageAsync(
            new Ark.MediatorFramework.Sample.GrpcClient.UpdateGreetingMessageRequest
            {
                Id = read.Id,
                Message = "stale",
                ETag = originalETag,
            }, metadata).ResponseAsync.ConfigureAwait(false);
        (await stale.Should().ThrowAsync<RpcException>().ConfigureAwait(false)).Which.StatusCode
            .Should().Be(StatusCode.FailedPrecondition);

        var reread = await client.GetGreetingAsync(
            new Ark.MediatorFramework.Sample.GrpcClient.GetGreetingQuery { Id = read.Id }, metadata).ResponseAsync.ConfigureAwait(false);
        var retried = await client.UpdateGreetingMessageAsync(
            new Ark.MediatorFramework.Sample.GrpcClient.UpdateGreetingMessageRequest
            {
                Id = reread.Id,
                Message = "updated twice",
                ETag = reread.ETag,
            }, metadata).ResponseAsync.ConfigureAwait(false);
        retried.Message.Should().Be("updated twice");
    }

    /// <summary>Verifies body-token fallback, malformed preconditions, and bounded retries.</summary>
    [TestMethod]
    public async Task BodyTokenAndRetrySemanticsAreEnforced()
    {
        const int MaxRetries = 2;
        const int ExhaustedRetries = MaxRetries + 1;
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtTokenBuilder().AddSubject("retry-user").AddScope("greetings.write").Build());
        var created = await context.Client.PostAsJsonAsync("/api/v1/greetings", new { name = "retry" }).ConfigureAwait(false);
        var greeting = await created.Content.ReadFromJsonAsync<GreetingResponse>(JsonOptions).ConfigureAwait(false);
        var current = await context.Client.GetFromJsonAsync<GreetingResponse>(
            $"/api/v1/greetings/{greeting!.Id}", JsonOptions).ConfigureAwait(false);

        using var bodyOnly = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{current!.Id}")
        {
            Content = JsonContent.Create(new { id = current.Id, message = "body", etag = current.ETag }),
        };
        (await context.Client.SendAsync(bodyOnly).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var invalid = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{current.Id}")
        {
            Content = JsonContent.Create(new { id = current.Id, message = "invalid", etag = "not-base64" }),
        };
        (await context.Client.SendAsync(invalid).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        var latest = await context.Client.GetFromJsonAsync<GreetingResponse>(
            $"/api/v1/greetings/{current.Id}", JsonOptions).ConfigureAwait(false);
        context.FaultInjector.PendingFailures = MaxRetries;
        using var retry = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{latest!.Id}")
        {
            Content = JsonContent.Create(new { id = latest.Id, message = "retried", etag = latest.ETag }),
        };
        (await context.Client.SendAsync(retry).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.OK);

        var afterRetry = await context.Client.GetFromJsonAsync<GreetingResponse>(
            $"/api/v1/greetings/{latest.Id}", JsonOptions).ConfigureAwait(false);
        context.FaultInjector.PendingFailures = ExhaustedRetries;
        using var exhausted = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/greetings/{latest.Id}")
        {
            Content = JsonContent.Create(new { id = latest.Id, message = "exhausted", etag = afterRetry!.ETag }),
        };
        (await context.Client.SendAsync(exhausted).ConfigureAwait(false)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
