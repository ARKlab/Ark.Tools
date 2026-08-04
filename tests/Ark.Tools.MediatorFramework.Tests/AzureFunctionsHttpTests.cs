// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.AzureFunctions;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
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
    public async Task StreamsJsonArrayWithoutBuffering()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await ArkAzureFunctionsHttp.WriteJsonStreamAsync(
            context.Response,
            Values(),
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
        json!["results"]!.AsObject().Should().BeEmpty();
    }

    [TestMethod]
    public async Task HealthCheckWritesFailedCheckDetails()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks()
            .AddCheck("failing", () => HealthCheckResult.Unhealthy("broken", data: new Dictionary<string, object>(StringComparer.Ordinal)
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
        json["results"]!["failing"]!["status"]!.GetValue<string>().Should().Be("Unhealthy");
        json["results"]!["failing"]!["description"]!.GetValue<string>().Should().Be("broken");
        json["results"]!["failing"]!["data"]!["answer"]!.GetValue<int>().Should().Be(42);
    }

    private static async IAsyncEnumerable<int> Values()
    {
        yield return 1;
        await Task.Yield();
        yield return 2;
    }
}
