// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using AwesomeAssertions;

using Grpc.Net.Client;

using Microsoft.AspNetCore.TestHost;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated gRPC unary clients dispatch synthetic contracts.</summary>
[TestClass]
public sealed class GrpcUnaryTests
{
    /// <summary>Verifies generated request and query clients bind all unary fields.</summary>
    [TestMethod]
    public async Task BindsGeneratedUnaryRequestAndQuery()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
        var client = new HostingV1.HostingV1Client(channel);

        var request = await client.HostingRequestAsync(
            new HostingRequest { Id = 42, Filter = "query", Value = "body" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);
        var query = await client.HostingQueryAsync(
            new HostingQuery { Id = 7, Value = "value" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        request.Message.Should().Be("42:query:body");
        request.ServerStamp.Should().Be("hosting-server");
        query.Message.Should().Be("7:value");
    }

    /// <summary>Verifies commands use the generated client and return the empty protobuf response.</summary>
    [TestMethod]
    public async Task DispatchesGeneratedCommand()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
        var client = new HostingV1.HostingV1Client(channel);

        var result = await client.ExecuteHostingCommandAsync(
            new HostingCommand { Value = "command" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        result.Should().NotBeNull();
        fixture.State.CommandExecutions.Should().Be(1);
    }

    /// <summary>Verifies generated versioned services expose only active versioned methods.</summary>
    [TestMethod]
    public async Task DispatchesActiveVersionedService()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
        var client = new HostingV2.HostingV2Client(channel);

        var result = await client.HostingVersionedQueryAsync(
            new HostingVersionedQuery { Id = 5, Value = "v2" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        result.Message.Should().Be("5:v2");
    }

    /// <summary>Verifies generated protobuf clients preserve NodaTime and polymorphic fields.</summary>
    [TestMethod]
    public async Task PreservesWireTypes()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
        var client = new HostingV1.HostingV1Client(channel);

        var result = await client.HostingWireTypesQueryAsync(
            new HostingWireTypesQuery(),
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        result.Date.Year.Should().Be(HostingWireTypeValues.Date.Year);
        result.Date.Month.Should().Be(HostingWireTypeValues.Date.Month);
        result.Date.Day.Should().Be(HostingWireTypeValues.Date.Day);
        result.DateTime.Hours.Should().Be(HostingWireTypeValues.DateTime.Hour);
        result.Shape.HostingCircle.Radius.Should().Be(HostingWireTypeValues.CircleRadius);
    }
}
