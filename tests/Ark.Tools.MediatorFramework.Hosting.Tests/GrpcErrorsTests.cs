// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;
using HostingContracts = Ark.Tools.MediatorFramework.Hosting.Contracts;

using AwesomeAssertions;

using Google.Rpc;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated gRPC rich error mappings.</summary>
[TestClass]
public sealed class GrpcErrorsTests
{
    /// <summary>Verifies validation failures contain google.rpc.BadRequest details.</summary>
    [TestMethod]
    public async Task MapsValidationFailureToRichStatus()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        var action = async () => await client.ValidateHostingRequestAsync(
            new HostingValidationRequest { Value = "invalid" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        var status = _readRichStatus(exception.Which);
        exception.Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
        status.Message.Should().Be("Validation failed");
        var badRequest = status.Details.Single(static detail => detail.Is(BadRequest.Descriptor)).Unpack<BadRequest>();
        badRequest.FieldViolations.Should().ContainSingle(static violation =>
            violation.Field == nameof(HostingContracts.HostingValidationRequest.Value)
            && violation.Description == "The synthetic value is invalid.");
    }

    /// <summary>Verifies business violations contain the framework protobuf detail.</summary>
    [TestMethod]
    public async Task MapsBusinessViolationToRichStatus()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        var action = async () => await client.TriggerHostingBusinessViolationAsync(
            new HostingBusinessViolationRequest { Value = "invalid" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        var status = _readRichStatus(exception.Which);
        exception.Which.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        var violation = status.Details
            .Single(static detail => detail.Is(ArkBusinessRuleViolation.Descriptor))
            .Unpack<ArkBusinessRuleViolation>();
        violation.Type.Should().Be("BusinessRuleViolation");
        violation.Title.Should().Be("Synthetic rule");
        violation.Status.Should().Be(422);
        violation.Detail.Should().Be("The synthetic business rule was violated.");
    }

    /// <summary>Verifies null handler results include a rich not-found status.</summary>
    [TestMethod]
    public async Task MapsNotFoundResultToGrpcFailure()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        var action = async () => await client.GetHostingNotFoundAsync(
            new HostingNotFoundQuery(),
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        var status = _readRichStatus(exception.Which);
        exception.Which.StatusCode.Should().Be(StatusCode.NotFound);
        status.Code.Should().Be((int)StatusCode.NotFound);
        status.Message.Should().Be("The requested resource was not found.");
    }

    /// <summary>Verifies opaque ETag failures use failed-precondition status.</summary>
    [TestMethod]
    public async Task MapsETagMismatchToFailedPrecondition()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        var action = async () => await client.CheckHostingETagAsync(
            new HostingETagMismatchRequest { ETag = "stale" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        exception.Which.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        exception.Which.Status.Detail.Should().Be("The synthetic ETag does not match.");
    }

    /// <summary>Verifies optimistic concurrency failures use aborted status.</summary>
    [TestMethod]
    public async Task MapsConcurrencyFailureToAborted()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        var action = async () => await client.CheckHostingConcurrencyAsync(
            new HostingOptimisticConcurrencyRequest { ETag = "stale" },
            cancellationToken: app.Lifetime.ApplicationStopping).ResponseAsync.ConfigureAwait(false);

        var exception = await action.Should().ThrowAsync<RpcException>().ConfigureAwait(false);
        exception.Which.StatusCode.Should().Be(StatusCode.Aborted);
        exception.Which.Status.Detail.Should().Be("The synthetic entity was concurrently modified.");
    }

    private static GrpcChannel _createChannel(WebApplication app)
    {
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
    }

    private static Google.Rpc.Status _readRichStatus(RpcException exception)
    {
        var bytes = exception.Trailers.GetValueBytes("grpc-status-details-bin");
        bytes.Should().NotBeNull();
        return Google.Rpc.Status.Parser.ParseFrom(bytes);
    }
}
