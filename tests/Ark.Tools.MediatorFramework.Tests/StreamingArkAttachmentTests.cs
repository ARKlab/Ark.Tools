// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text;

using Ark.MediatorFramework;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class StreamingArkAttachmentTests
{
    [TestMethod]
    public async Task OpenReadAsyncReadsMetadataAndAllChunks()
    {
        var attachment = new StreamingArkAttachment(ChunksAsync());
        await using var stream = attachment.OpenRead();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var content = await reader.ReadToEndAsync();

        attachment.Name.Should().Be("document.txt");
        attachment.ContentType.Should().Be("text/plain");
        content.Should().Be("first-second");
    }

    [TestMethod]
    public async Task OpenReadAsyncRejectsMissingMetadata()
    {
        var attachment = new StreamingArkAttachment(MissingMetadataAsync());
        await using var stream = attachment.OpenRead();

        var action = async () => await stream.ReadAsync(new byte[8]);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    private static async IAsyncEnumerable<UploadDocumentChunk> ChunksAsync()
    {
        yield return new UploadDocumentChunk
        {
            Metadata = new UploadDocumentMetadata { Name = "document.txt", ContentType = "text/plain" },
        };
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("first-") };
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("second") };
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<UploadDocumentChunk> MissingMetadataAsync()
    {
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("invalid") };
        await Task.CompletedTask;
    }
}
