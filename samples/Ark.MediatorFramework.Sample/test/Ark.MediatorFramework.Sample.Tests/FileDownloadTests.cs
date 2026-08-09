// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application;
using Ark.MediatorFramework.Sample.Tests.Hooks;
using Ark.Tools.Core;

using AwesomeAssertions;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies attachment storage and retrieval through application contracts.</summary>
[TestClass]
public sealed class FileDownloadTests
{
    /// <summary>Stores and retrieves the same attachment bytes and metadata.</summary>
    [TestMethod]
    public async Task UploadThenRetrieveReturnsSameAttachment()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        context.SetAuthenticatedUser("file-user");
        var id = Guid.NewGuid();
        var bytes = new byte[] { 0, 1, 2, 254, 255 };
        var attachment = new ArkAttachment(
            "document.bin",
            "application/octet-stream",
            () => new MemoryStream(bytes, writable: false));

        var upload = await context.DispatchRequestAsync<UploadGreetingCardRequest, UploadResponse>(
            new UploadGreetingCardRequest
            {
                Id = id,
                Label = "file",
                Attachment = attachment,
            }).ConfigureAwait(false);
        var stored = await context.DispatchQueryAsync<GetDocumentQuery, IArkAttachment>(
            new GetDocumentQuery { Id = id }).ConfigureAwait(false);

        upload.Length.Should().Be(bytes.Length);
        stored.Name.Should().Be("document.bin");
        stored.ContentType.Should().Be("application/octet-stream");
        await using var stream = stored.OpenRead();
        using var result = new MemoryStream();
        await stream.CopyToAsync(result).ConfigureAwait(false);
        result.ToArray().Should().Equal(bytes);
    }

    /// <summary>Preserves attachment order in a batch upload.</summary>
    [TestMethod]
    public async Task BatchUploadPreservesAttachmentOrder()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);
        var result = await context.DispatchRequestAsync<UploadGreetingCardsRequest, UploadBatchResponse>(
            new UploadGreetingCardsRequest
            {
                Id = Guid.NewGuid(),
                Attachments =
                [
                    Attachment("first.txt", "one"),
                    Attachment("second.txt", "two"),
                ],
            }).ConfigureAwait(false);

        result.Names.Should().Equal("first.txt", "second.txt");
    }

    /// <summary>Reports a typed not-found failure for an unknown attachment.</summary>
    [TestMethod]
    public async Task MissingAttachmentThrowsNotFound()
    {
        await using var context = new ApplicationTestContext(useSqlStore: false);

        var action = () => context.DispatchQueryAsync<GetDocumentQuery, IArkAttachment>(
            new GetDocumentQuery { Id = Guid.NewGuid() });

        await action.Should().ThrowAsync<EntityNotFoundException>().ConfigureAwait(false);
    }

    private static ArkAttachment Attachment(string name, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new ArkAttachment(name, "text/plain", () => new MemoryStream(bytes, writable: false));
    }
}
