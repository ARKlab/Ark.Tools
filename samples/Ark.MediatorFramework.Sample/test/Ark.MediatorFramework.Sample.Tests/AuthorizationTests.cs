// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.GrpcClient;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

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
            new GetGreetingQuery { Id = 1 }).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>();
        exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }
}
