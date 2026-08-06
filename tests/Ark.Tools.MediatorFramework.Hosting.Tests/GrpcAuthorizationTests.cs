// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves gRPC metadata authentication, authorization, and user context.</summary>
[TestClass]
public sealed class GrpcAuthorizationTests
{
    /// <summary>Verifies anonymous gRPC calls are rejected.</summary>
    [TestMethod]
    public async Task RejectsAnonymousCaller()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = CreateChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        var action = async () => await client.HostingAuthorizedQueryAsync(
            new HostingAuthorizedQuery(),
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        exception.Which.StatusCode.Should().Be(StatusCode.PermissionDenied);
        fixture.State.AuthorizedExecutions.Should().Be(0);
    }

    /// <summary>Verifies authenticated callers without the required scope are rejected.</summary>
    [TestMethod]
    public async Task RejectsCallerWithoutRequiredScope()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = CreateChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        var headers = new Metadata { { "authorization", "\u0042earer\u0020authenticated" } };

        var action = async () => await client.HostingAuthorizedQueryAsync(
            new HostingAuthorizedQuery(), headers, cancellationToken: app.Lifetime.ApplicationStopping)
            .ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        exception.Which.StatusCode.Should().Be(StatusCode.PermissionDenied);
        fixture.State.AuthorizedExecutions.Should().Be(0);
    }

    /// <summary>Verifies authorized metadata reaches the handler.</summary>
    [TestMethod]
    public async Task AllowsCallerWithRequiredScope()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = CreateChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        var headers = new Metadata { { "authorization", "\u0042earer\u0020scope" } };

        var result = await client.HostingAuthorizedQueryAsync(
            new HostingAuthorizedQuery(), headers, cancellationToken: app.Lifetime.ApplicationStopping)
            .ResponseAsync.ConfigureAwait(false);

        result.Message.Should().Be("authorized");
        fixture.State.AuthorizedExecutions.Should().Be(1);
    }

    /// <summary>Verifies authenticated gRPC metadata propagates into the user context.</summary>
    [TestMethod]
    public async Task PropagatesUserContext()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = CreateChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        var headers = new Metadata { { "authorization", "\u0042earer\u0020scope" } };

        var result = await client.HostingUserContextQueryAsync(
            new HostingUserContextQuery(), headers, cancellationToken: app.Lifetime.ApplicationStopping)
            .ResponseAsync.ConfigureAwait(false);

        result.Message.Should().Be("hosting-test-user");
    }

    private static GrpcChannel CreateChannel(WebApplication app)
    {
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
    }
}
