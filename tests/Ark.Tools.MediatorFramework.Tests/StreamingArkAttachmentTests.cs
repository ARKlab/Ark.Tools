// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

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

        var content = await reader.ReadToEndAsync().ConfigureAwait(false);

        attachment.Name.Should().Be("document.txt");
        attachment.ContentType.Should().Be("text/plain");
        content.Should().Be("first-second");
    }

    [TestMethod]
    public async Task OpenReadAsyncRejectsMissingMetadata()
    {
        var attachment = new StreamingArkAttachment(MissingMetadataAsync());
        await using var stream = attachment.OpenRead();

        var action = async () => await stream.ReadAsync(new byte[8]).ConfigureAwait(false);

        await action.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ReadAllAsyncReadsMetadataDelimitedFilesInOrder()
    {
        var attachments = await StreamingArkAttachmentCollection.ReadAllAsync(MultipleChunksAsync()).ConfigureAwait(false);

        attachments.Select(attachment => attachment.Name).Should().Equal("first.txt", "second.txt");
        attachments.Select(attachment => attachment.ContentType).Should().Equal("text/plain", "text/plain");
        await using var first = attachments[0].OpenRead();
        await using var second = attachments[1].OpenRead();
        using var firstReader = new StreamReader(first, Encoding.UTF8);
        using var secondReader = new StreamReader(second, Encoding.UTF8);
        (await firstReader.ReadToEndAsync().ConfigureAwait(false)).Should().Be("one");
        (await secondReader.ReadToEndAsync().ConfigureAwait(false)).Should().Be("two");
    }

    private static async IAsyncEnumerable<UploadDocumentChunk> ChunksAsync()
    {
        yield return new UploadDocumentChunk
        {
            Metadata = new UploadDocumentMetadata { Name = "document.txt", ContentType = "text/plain" },
        };
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("first-") };
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("second") };
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<UploadDocumentChunk> MissingMetadataAsync()
    {
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("invalid") };
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<UploadDocumentChunk> MultipleChunksAsync()
    {
        yield return new UploadDocumentChunk
        {
            Metadata = new UploadDocumentMetadata { Name = "../../first.txt", ContentType = "text/plain" },
        };
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("one") };
        yield return new UploadDocumentChunk
        {
            Metadata = new UploadDocumentMetadata { Name = "../../second.txt", ContentType = "text/plain" },
        };
        yield return new UploadDocumentChunk { Data = Encoding.UTF8.GetBytes("two") };
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
