// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;

using System.Net;
using System.Net.Http.Headers;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated multipart attachment binding and limits.</summary>
[TestClass]
public sealed class MinimalApiAttachmentsTests
{
    /// <summary>Verifies a valid single attachment reaches the handler with its content.</summary>
    [TestMethod]
    public async Task BindsSingleAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = CreateMultipart(
            ("attachment", "hello.txt", "text/plain", "hello attachment"));

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/attachments"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.State.LastAttachmentName.Should().Be("hello.txt");
        fixture.State.LastAttachmentContent.Should().Be("hello attachment");
    }

    /// <summary>Verifies a collection of attachments is bound in order.</summary>
    [TestMethod]
    public async Task BindsAttachmentCollection()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = CreateMultipart(
            ("attachments", "one.txt", "text/plain", "one"),
            ("attachments", "two.txt", "text/plain", "two"));

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/attachments/multiple"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fixture.State.LastAttachmentCount.Should().Be(2);
    }

    /// <summary>Verifies single-file count limits reject requests before dispatch.</summary>
    [TestMethod]
    public async Task RejectsTooManyFilesForSingleAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = CreateMultipart(
            ("attachment", "one.txt", "text/plain", "one"),
            ("attachment", "two.txt", "text/plain", "two"));

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/attachments"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fixture.State.LastAttachmentName.Should().BeNull();
        fixture.State.LastAttachmentContent.Should().BeNull();
    }

    /// <summary>Verifies collection file-count limits reject requests before dispatch.</summary>
    [TestMethod]
    public async Task RejectsTooManyFilesForAttachmentCollection()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = CreateMultipart(
            ("attachments", "one.txt", "text/plain", "one"),
            ("attachments", "two.txt", "text/plain", "two"),
            ("attachments", "three.txt", "text/plain", "three"));

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/attachments/multiple"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        fixture.State.LastAttachmentCount.Should().Be(0);
    }

    /// <summary>Verifies content-type limits reject unsupported files before dispatch.</summary>
    [TestMethod]
    public async Task RejectsUnsupportedAttachmentContentType()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = CreateMultipart(
            ("attachment", "payload.bin", "application/octet-stream", "payload"));

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/attachments"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        fixture.State.LastAttachmentName.Should().BeNull();
    }

    /// <summary>Verifies request-size limits reject oversized multipart bodies before dispatch.</summary>
    [TestMethod]
    public async Task RejectsOversizedAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();
        using var content = CreateMultipart(
            ("attachment", "large.txt", "text/plain", new string('x', 2048)));

        using var response = await client.PostAsync(
            new Uri("http://localhost/hosting/attachments"),
            content,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be((HttpStatusCode)413);
        fixture.State.LastAttachmentName.Should().BeNull();
    }

    /// <summary>Verifies downloadable attachments preserve content type, name, and bytes.</summary>
    [TestMethod]
    public async Task DownloadsAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMinimalApiHostAsync().ConfigureAwait(false);
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync(
            new Uri("http://localhost/hosting/attachments/download.txt"),
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("\"download.txt\"");
        (await response.Content.ReadAsStringAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false))
            .Should().Be("downloaded content");
    }

    private static MultipartFormDataContent CreateMultipart(
        params (string Name, string FileName, string ContentType, string Content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var file in files)
        {
#pragma warning disable CA2000
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(file.Content));
#pragma warning restore CA2000
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(content, file.Name, file.FileName);
        }

        return form;
    }
}
