// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.GrpcClient;
using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.MediatorFramework.Sample.Tests.Auth;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;
using Google.Protobuf;

using System.Net.Http.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies authorization on the sample's transport endpoints.</summary>
[TestClass]
public sealed class AuthorizationTests
{
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
            new GetGreetingQuery { Id = ByteString.Empty }).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    /// <summary>A bearer token without the contract policy claim cannot invoke the mutation.</summary>
    [TestMethod]
    public async Task HttpCallWithoutGreetingWriteScopeReturnsForbidden()
    {
        using var context = new SampleTestContext();
        context.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("test-user").Build());

        var response = await context.Client.PostAsJsonAsync(
            "/api/v1/greetings",
            new CreateGreetingRequest { Name = "policy-test" }).ConfigureAwait(false);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }
}
