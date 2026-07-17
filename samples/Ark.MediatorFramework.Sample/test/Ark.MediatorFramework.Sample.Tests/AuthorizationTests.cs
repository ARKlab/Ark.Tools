// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.GrpcClient;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.MediatorFramework.Sample.Tests.Auth;
using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;
using Google.Protobuf;

using GrpcGetGreetingQuery = Ark.MediatorFramework.Sample.GrpcClient.GetGreetingQuery;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies authorization on the sample's transport endpoints.</summary>
[TestClass]
public sealed class AuthorizationTests
{
    /// <summary>Malformed bearer credentials are rejected without exposing a server error.</summary>
    [TestMethod]
    public async Task HttpCallWithMalformedBearerReturnsUnauthorized()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            "not-a-jwt");

        var response = await context.Client.GetAsync(
            new Uri($"/api/v1/greetings/{Guid.Empty}", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>Unsupported authorization schemes are rejected.</summary>
    [TestMethod]
    public async Task HttpCallWithBasicCredentialsReturnsUnauthorized()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            "abc");

        var response = await context.Client.GetAsync(
            new Uri($"/api/v1/greetings/{Guid.Empty}", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>Requests without authorization credentials are rejected.</summary>
    [TestMethod]
    public async Task HttpCallWithoutCredentialsReturnsUnauthorized()
    {
        using var context = SampleTestContext.WithoutFallbackPolicy();

        var response = await context.Client.GetAsync(
            new Uri($"/api/v1/greetings/{Guid.Empty}", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    /// <summary>Authenticated callers can invoke a generated endpoint successfully.</summary>
    [TestMethod]
    public async Task HttpCallWithValidBearerReturnsSuccess()
    {
        using var context = SampleTestContext.WithoutFallbackPolicy();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").Build());

        using var content = new StringContent(
            """{"id":"00000000-0000-0000-0000-000000000000"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await context.Client.PostAsync(
            new Uri("/api/v1/greetings/refresh", UriKind.Relative),
            content).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    /// <summary>Generated gRPC endpoints reject calls without bearer metadata.</summary>
    [TestMethod]
    public async Task GrpcCallWithoutBearerMetadataReturnsUnauthenticated()
    {
        using var context = new SampleTestContext();
        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpHandler = context.CreateGrpcHandler() });
        var client = new GreetingsV1.GreetingsV1Client(channel);

        var action = async () => await client.GetGreetingAsync(
            new GrpcGetGreetingQuery { Id = ByteString.Empty }).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    /// <summary>Authenticated gRPC calls expose the bearer principal to mediator handlers.</summary>
    [TestMethod]
    public async Task GrpcCallWithValidBearerFlowsUserContext()
    {
        using var context = new SampleTestContext();
        var token = new JwtTokenBuilder().AddSubject("grpc-user").AddScope("greetings.write").Build();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            token);
        using var channel = GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions { HttpClient = context.Client });

        var response = await new GreetingsV1.GreetingsV1Client(channel).CreateGreetingAsync(
            new CreateGreetingRequest { Name = "grpc-context" },
            new Metadata { { "Authorization", string.Concat("Bearer ", token) } }).ResponseAsync.ConfigureAwait(false);

        response.Message.Should().Contain("grpc-user");
    }

    /// <summary>A bearer token without the contract policy claim cannot invoke the mutation.</summary>
    [TestMethod]
    public async Task HttpCallWithoutGreetingWriteScopeReturnsForbidden()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").Build());

        using var content = new StringContent(
            """{"name":"policy-test","date":"2024-01-01","dateTime":"2024-01-01T00:00:00","offsetDateTime":"2024-01-01T00:00:00Z","period":"P0D"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await context.Client.PostAsync(new Uri("/api/v1/greetings", UriKind.Relative), content).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    /// <summary>HTTP-only commands execute inline and return no content.</summary>
    [TestMethod]
    public async Task HttpCommandReturnsNoContent()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").Build());

        using var content = new StringContent(
            """{"id":"00000000-0000-0000-0000-000000000000"}""",
            System.Text.Encoding.UTF8,
            "application/json");
        var response = await context.Client.PostAsync(
            new Uri("/api/v1/greetings/refresh", UriKind.Relative),
            content).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }
}
