// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Grpc;
using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using AwesomeAssertions;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated gRPC client-streaming attachment behavior.</summary>
[TestClass]
public sealed class GrpcUploadTests
{
    /// <summary>Verifies metadata-first chunks reach the attachment handler incrementally.</summary>
    [TestMethod]
    public async Task UploadsMetadataAndDataChunks()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = CreateChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        using var call = client.UploadHostingAttachment(
            cancellationToken: app.Lifetime.ApplicationStopping);

        await call.RequestStream.WriteAsync(new UploadDocumentChunk
        {
            Metadata = new UploadDocumentMetadata { Name = "document.txt", ContentType = "text/plain" },
        }, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await call.RequestStream.WriteAsync(new UploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFromUtf8("first-"),
        }, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await call.RequestStream.WriteAsync(new UploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFromUtf8("second"),
        }, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);

        var result = await call.ResponseAsync.ConfigureAwait(false);

        result.Message.Should().Be("document.txt");
        fixture.State.LastAttachmentName.Should().Be("document.txt");
        fixture.State.LastAttachmentContent.Should().Be("first-second");
    }

    /// <summary>Verifies upload streams reject a data chunk before metadata.</summary>
    [TestMethod]
    public async Task RejectsDataBeforeMetadata()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = CreateChannel(app);
        var client = new HostingV1.HostingV1Client(channel);
        using var call = client.UploadHostingAttachment(
            cancellationToken: app.Lifetime.ApplicationStopping);
        await call.RequestStream.WriteAsync(new UploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFromUtf8("invalid"),
        }, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);

        RpcException? exception = null;
        try
        {
            await call.ResponseAsync.ConfigureAwait(false);
        }
        catch (RpcException caught)
        {
            exception = caught;
        }

        exception.Should().NotBeNull();
        exception!.StatusCode.Should().Be(StatusCode.Unknown);
        fixture.State.LastAttachmentName.Should().BeNull();
    }

    private static GrpcChannel CreateChannel(WebApplication app)
    {
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
    }
}
