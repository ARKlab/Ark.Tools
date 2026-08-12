// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using AwesomeAssertions;

using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated gRPC server streaming and cancellation behavior.</summary>
[TestClass]
public sealed class GrpcStreamingTests
{
    /// <summary>Verifies generated clients receive server-streamed items incrementally.</summary>
    [TestMethod]
    public async Task StreamsItemsIncrementally()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        using var call = client.StreamHosting(
            new HostingStreamQuery { Count = 3 },
            cancellationToken: app.Lifetime.ApplicationStopping);

        var items = new List<int>();
        while (await call.ResponseStream.MoveNext(app.Lifetime.ApplicationStopping).ConfigureAwait(false))
            items.Add(call.ResponseStream.Current.Number);

        items.Should().Equal(1, 2, 3);
    }

    /// <summary>Verifies an empty server stream completes without items.</summary>
    [TestMethod]
    public async Task StreamsEmptySequence()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        using var call = client.StreamHosting(
            new HostingStreamQuery { Count = 0 },
            cancellationToken: app.Lifetime.ApplicationStopping);

        (await call.ResponseStream.MoveNext(app.Lifetime.ApplicationStopping).ConfigureAwait(false))
            .Should().BeFalse();
    }

    /// <summary>Verifies cancellation reaches a paused stream producer.</summary>
    [TestMethod]
    public async Task PropagatesCancellationToProducer()
    {
        await using var fixture = new HostingTestFixture();
        fixture.State.HoldStreamAfterFirst = true;
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        using var cts = new CancellationTokenSource();
        using var call = client.StreamHosting(new HostingStreamQuery { Count = 2 }, cancellationToken: cts.Token);

        (await call.ResponseStream.MoveNext(app.Lifetime.ApplicationStopping).ConfigureAwait(false))
            .Should().BeTrue();
        call.ResponseStream.Current.Number.Should().Be(1);
        fixture.State.StreamFirstItemProduced.IsCompleted.Should().BeTrue();

        await cts.CancelAsync().ConfigureAwait(false);

        await fixture.State.StreamCancellationObserved.WaitAsync(
            TimeSpan.FromSeconds(5),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
    }

    private static GrpcChannel _createChannel(WebApplication app)
    {
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
    }
}
