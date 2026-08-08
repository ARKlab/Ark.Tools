// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Tests.Auth;
using Ark.MediatorFramework.Sample.Tests.Hooks;

using AwesomeAssertions;

using System.Net;
using System.Net.Http.Headers;
namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies generated attachment download behavior.</summary>
[TestClass]
public sealed class FileDownloadTests
{
    /// <summary>Multipart collections preserve form order and metadata.</summary>
    [TestMethod]
    public async Task MultiFileUploadPreservesFormOrder()
    {
        using var context = TransportTestContext.WithoutFallbackPolicy();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtTokenBuilder().AddSubject("file-user").Build());
        using var form = new MultipartFormDataContent();
        using var first = new StringContent("one");
        using var second = new StringContent("two");
        first.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        second.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(first, "Attachments", "../../first.txt");
        form.Add(second, "Attachments", "../../second.txt");

        var response = await context.Client.PostAsync(new Uri($"/api/v1/greeting-cards/{Guid.NewGuid()}/batch", UriKind.Relative), form).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        body.Should().Contain("\"names\":[\"first.txt\",\"second.txt\"]");
    }

    /// <summary>Batch uploads reject files beyond the declared limit.</summary>
    [TestMethod]
    public async Task MultiFileUploadRejectsTooManyFiles()
    {
        using var context = TransportTestContext.WithoutFallbackPolicy();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtTokenBuilder().AddSubject("file-user").Build());
        using var form = new MultipartFormDataContent();
        var files = new List<StringContent>();
        for (var index = 0; index < 5; index++)
        {
            var file = new StringContent(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            files.Add(file);
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file, "Attachments", $"file-{index}.txt");
        }

        var response = await context.Client.PostAsync(new Uri($"/api/v1/greeting-cards/{Guid.NewGuid()}/batch", UriKind.Relative), form).ConfigureAwait(false);
        foreach (var file in files)
            file.Dispose();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Uploaded bytes are returned with safe file metadata.</summary>
    [TestMethod]
    public async Task UploadThenDownloadReturnsSameBytes()
    {
        using var context = TransportTestContext.WithoutFallbackPolicy();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("file-user").Build());
        var id = Guid.NewGuid();
        var bytes = new byte[] { 0, 1, 2, 254, 255 };
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "Attachment", "../document.bin");

        var upload = await context.Client.PostAsync(new Uri($"/api/v1/greeting-cards/{id}?Label=file", UriKind.Relative), form).ConfigureAwait(false);
        upload.StatusCode.Should().Be(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync().ConfigureAwait(false));

        var download = await context.Client.GetAsync(new Uri($"/api/v1/greeting-cards/{id}/download", UriKind.Relative)).ConfigureAwait(false);
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        download.Content.Headers.ContentDisposition.FileName.Should().Be("document.bin");
        (await download.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).Should().Equal(bytes);
    }

    /// <summary>Unknown attachments return not found.</summary>
    [TestMethod]
    public async Task MissingDownloadReturnsNotFound()
    {
        using var context = TransportTestContext.WithoutFallbackPolicy();
        context.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtTokenBuilder().AddSubject("file-user").Build());

        var response = await context.Client.GetAsync(new Uri($"/api/v1/greeting-cards/{Guid.NewGuid()}/download", UriKind.Relative)).ConfigureAwait(false);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
