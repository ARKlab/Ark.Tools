// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient;

using GrpcUploadDocumentChunk = Ark.Tools.MediatorFramework.Grpc.UploadDocumentChunk;
using GrpcUploadDocumentMetadata = Ark.Tools.MediatorFramework.Grpc.UploadDocumentMetadata;

using GrpcDownloadDocumentChunk = Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient.DownloadDocumentChunk;
using GrpcDownloadDocumentMetadata = Ark.Tools.MediatorFramework.Hosting.Contracts.GrpcClient.DownloadDocumentMetadata;

using AwesomeAssertions;

using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;


namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated gRPC server-streaming attachment download behavior.</summary>
[TestClass]
public sealed class GrpcDownloadTests
{
    /// <summary>Verifies a download streams a metadata chunk followed by the attachment bytes.</summary>
    [TestMethod]
    public async Task DownloadsMetadataAndDataChunks()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        using var call = client.DownloadHostingAttachment(
            new HostingAttachmentDownloadQuery { Name = "download.txt" },
            cancellationToken: app.Lifetime.ApplicationStopping);
        var (metadata, content) = await _readDownloadAsync(call, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        metadata.Should().NotBeNull();
        metadata!.Name.Should().Be("download.txt");
        metadata.ContentType.Should().Be("text/plain");
        content.Should().Be("downloaded content");
    }

    /// <summary>Verifies an uploaded attachment round-trips byte-for-byte through gRPC download.</summary>
    [TestMethod]
    public async Task RoundTripsUploadedAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        using var upload = client.UploadHostingAttachment(
            cancellationToken: app.Lifetime.ApplicationStopping);
        await upload.RequestStream.WriteAsync(new GrpcUploadDocumentChunk
        {
            Metadata = new GrpcUploadDocumentMetadata { Name = "roundtrip.txt", ContentType = "text/plain" },
        }, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await upload.RequestStream.WriteAsync(new GrpcUploadDocumentChunk
        {
            Data = Google.Protobuf.ByteString.CopyFromUtf8("round-trip payload"),
        }, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        await upload.RequestStream.CompleteAsync().ConfigureAwait(false);
        await upload.ResponseAsync.ConfigureAwait(false);

        using var download = client.DownloadHostingAttachment(
            new HostingAttachmentDownloadQuery { Name = "roundtrip.txt" },
            cancellationToken: app.Lifetime.ApplicationStopping);
        var (metadata, content) = await _readDownloadAsync(download, app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        metadata!.Name.Should().Be("roundtrip.txt");
        content.Should().Be("round-trip payload");
    }

    /// <summary>Verifies a missing attachment yields an empty download stream.</summary>
    [TestMethod]
    public async Task ReturnsEmptyStreamForMissingAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartGrpcHostAsync().ConfigureAwait(false);
        using var channel = _createChannel(app);
        var client = new HostingV1.HostingV1Client(channel);

        using var call = client.DownloadHostingAttachment(
            new HostingAttachmentDownloadQuery { Name = "missing.txt" },
            cancellationToken: app.Lifetime.ApplicationStopping);
        var chunks = 0;
        while (await call.ResponseStream.MoveNext(app.Lifetime.ApplicationStopping).ConfigureAwait(false))
            chunks++;

        chunks.Should().Be(0);
    }

    private static async Task<(GrpcDownloadDocumentMetadata? Metadata, string Content)> _readDownloadAsync(
        global::Grpc.Core.AsyncServerStreamingCall<GrpcDownloadDocumentChunk> call,
        CancellationToken ctk)
    {
        GrpcDownloadDocumentMetadata? metadata = null;
        var content = new StringBuilder();
        while (await call.ResponseStream.MoveNext(ctk).ConfigureAwait(false))
        {
            var chunk = call.ResponseStream.Current;
            if (chunk.ContentCase == GrpcDownloadDocumentChunk.ContentOneofCase.Metadata)
            {
                metadata.Should().BeNull();
                content.Length.Should().Be(0);
                metadata = chunk.Metadata;
            }
            else
            {
                content.Append(chunk.Data.ToStringUtf8());
            }
        }

        return (metadata, content.ToString());
    }

    private static GrpcChannel _createChannel(WebApplication app)
    {
        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpClient = app.GetTestServer().CreateClient(),
        });
    }
}
