// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using System.Text.Json.Nodes;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies Azure Functions file and streaming helpers.</summary>
[TestClass]
public sealed class AzureFunctionsHttpTests
{
    [TestMethod]
    public async Task ReadsAndSanitizesMultipartAttachment()
    {
        var context = new DefaultHttpContext();
        var content = new MemoryStream("payload"u8.ToArray());
        var form = new FormFile(content, 0, content.Length, "file", "../unsafe.txt")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain",
        };
        context.Request.ContentType = "multipart/form-data; boundary=test";
        context.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.OrdinalIgnoreCase),
            new FormFileCollection { form });

        var attachments = await ArkAzureFunctionsHttp.ReadAttachmentsAsync(
            context.Request,
            1,
            Array.Empty<string>(),
            CancellationToken.None);

        attachments.Should().HaveCount(1);
        attachments[0].Name.Should().Be("unsafe.txt");
    }

    [TestMethod]
    public async Task RejectsNonMultipartAttachmentRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "application/json";

        var act = () => ArkAzureFunctionsHttp.ReadAttachmentsAsync(
            context.Request, 1, Array.Empty<string>(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [TestMethod]
    public async Task RejectsAttachmentsExceedingFileCount()
    {
        var context = _multipartContext(_formFile("a.txt", "text/plain"), _formFile("b.txt", "text/plain"));

        var act = () => ArkAzureFunctionsHttp.ReadAttachmentsAsync(
            context.Request, 1, Array.Empty<string>(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [TestMethod]
    public async Task RejectsAttachmentWithDisallowedContentType()
    {
        var context = _multipartContext(_formFile("a.bin", "application/octet-stream"));

        var act = () => ArkAzureFunctionsHttp.ReadAttachmentsAsync(
            context.Request, 0, _pngOnly, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [TestMethod]
    public async Task EnforceMaxRequestBodySizeRejectsOversizedDeclaredBody()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentLength = 2048;

        var result = ArkAzureFunctionsHttp.EnforceMaxRequestBodySize(context.Request, 1024);

        result.Should().NotBeNull();
        var response = new DefaultHttpContext { RequestServices = _emptyServices() };
        response.Response.Body = new MemoryStream();
        await result!.ExecuteAsync(response);
        response.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
    }

    [TestMethod]
    public void EnforceMaxRequestBodySizeConfiguresFeatureForUndeclaredBody()
    {
        var context = new DefaultHttpContext();
        var feature = new FakeMaxRequestBodySizeFeature();
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(feature);

        var result = ArkAzureFunctionsHttp.EnforceMaxRequestBodySize(context.Request, 1024);

        result.Should().BeNull();
        feature.MaxRequestBodySize.Should().Be(1024);
    }

    [TestMethod]
    public void EnforceMaxRequestBodySizeAllowsBodyWithinLimit()
    {
        var context = new DefaultHttpContext();
        context.Request.ContentLength = 512;

        ArkAzureFunctionsHttp.EnforceMaxRequestBodySize(context.Request, 1024).Should().BeNull();
    }

    private static readonly string[] _pngOnly = ["image/png"];

    private static IServiceProvider _emptyServices()
    {
        return new ServiceCollection().AddLogging().BuildServiceProvider();
    }

    private static FormFile _formFile(string name, string contentType)
    {
        var content = new MemoryStream("payload"u8.ToArray());
        return new FormFile(content, 0, content.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    private static DefaultHttpContext _multipartContext(params FormFile[] files)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data; boundary=test";
        var collection = new FormFileCollection();
        collection.AddRange(files);
        context.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(StringComparer.OrdinalIgnoreCase),
            collection);
        return context;
    }

    private sealed class FakeMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;
        public long? MaxRequestBodySize { get; set; }
    }


    [TestMethod]
    public async Task StreamsJsonArrayWithoutBuffering()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await ArkAzureFunctionsHttp.WriteJsonStreamAsync(
            context.Response,
            _values(),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync(CancellationToken.None)).Should().Be("[1,2]");
    }

    [TestMethod]
    public async Task HealthCheckUsesRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddArkAzureFunctions();
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        context.Response.Body = new MemoryStream();

        var result = await ArkAzureFunctionsHttp.CheckHealthAsync(
            provider.GetRequiredService<HealthCheckService>(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store, no-cache");
        context.Response.Headers.Pragma.ToString().Should().Be("no-cache");
        context.Response.Headers.Expires.ToString().Should().Be("Thu, 01 Jan 1970 00:00:00 GMT");

        context.Response.Body.Position = 0;
        var json = await JsonNode.ParseAsync(context.Response.Body, cancellationToken: CancellationToken.None);
        json!["status"]!.GetValue<string>().Should().Be("Healthy");
        json!["entries"]!.AsObject().Should().BeEmpty();
    }

    [TestMethod]
    public async Task HealthCheckWritesFailedCheckDetails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck("failing", static () => HealthCheckResult.Unhealthy("broken", data: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["answer"] = 42,
            }));
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        context.Response.Body = new MemoryStream();

        var result = await ArkAzureFunctionsHttp.CheckHealthAsync(
            provider.GetRequiredService<HealthCheckService>(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.Body.Position = 0;
        var json = await JsonNode.ParseAsync(context.Response.Body, cancellationToken: CancellationToken.None);
        json!["status"]!.GetValue<string>().Should().Be("Unhealthy");
        json["entries"]!["failing"]!["status"]!.GetValue<string>().Should().Be("Unhealthy");
        json["entries"]!["failing"]!["description"]!.GetValue<string>().Should().Be("broken");
        json["entries"]!["failing"]!["data"]!["answer"]!.GetValue<int>().Should().Be(42);
    }

    private static async IAsyncEnumerable<int> _values()
    {
        yield return 1;
        await Task.Yield();
        yield return 2;
    }
}
