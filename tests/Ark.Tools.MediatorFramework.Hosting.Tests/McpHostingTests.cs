// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using System.Net.Http.Headers;

namespace Ark.Tools.MediatorFramework.Hosting.Tests;

/// <summary>Proves generated MCP registration, invocation, errors, and authorization.</summary>
[TestClass]
public sealed class McpHostingTests
{
#pragma warning disable MA0004 // IAsyncDisposable declarations do not expose a configured-dispose form here.
    /// <summary>Verifies the anonymous MCP client sees only generated anonymous tools.</summary>
    [TestMethod]
    public async Task ListsAnonymousTools()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

        tools.Select(static tool => tool.Name).Should().BeEquivalentTo(
            [
                "hosting.attachment.download",
                "hosting.attachment.upload",
                "hosting.query",
                "hosting.unexpected",
                "hosting.validation",
            ]);

        var query = tools.Single(static tool => tool.Name == "hosting.query");
        query.ProtocolTool.OutputSchema.Should().NotBeNull();
        query.ProtocolTool.OutputSchema!.Value.GetProperty("type").GetString().Should().Be("object");
        query.ProtocolTool.OutputSchema.Value.GetProperty("properties").GetProperty("message").GetProperty("type")
            .GetString().Should().Be("string");
    }

    /// <summary>Verifies an authenticated MCP client sees the protected generated tool.</summary>
    [TestMethod]
    public async Task ListsProtectedToolForAuthenticatedClient()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, "authenticated", "1").ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

        tools.Select(static tool => tool.Name).Should().Contain("hosting.authorized");
    }

    /// <summary>Verifies an anonymous client cannot invoke a protected generated tool.</summary>
    [TestMethod]
    public async Task RejectsProtectedToolForAnonymousClient()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var action = async () => await client.CallToolAsync(
            "hosting.authorized",
            new Dictionary<string, object?>(StringComparer.Ordinal),
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        await action.Should().ThrowAsync<McpException>().ConfigureAwait(false);
        fixture.State.AuthorizedExecutions.Should().Be(0);
    }

    /// <summary>Verifies generated query arguments dispatch to the mediator processor.</summary>
    [TestMethod]
    public async Task CallsGeneratedQuery()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.query",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 7, ["value"] = "from-mcp" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().NotBe(true);
        result.StructuredContent.Should().NotBeNull();
        result.StructuredContent!.Value.GetProperty("message").GetString().Should().Be("7:from-mcp");
    }

    /// <summary>Verifies validation failures become safe structured MCP errors.</summary>
    [TestMethod]
    public async Task MapsValidationFailureToMcpError()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.validation",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "invalid" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        ((TextContentBlock)result.Content[0]).Text.Should()
            .StartWith("Validation failed: ")
            .And.Contain("The synthetic value is invalid.");
        result.StructuredContent!.Value.GetProperty("title").GetString().Should().Be("Validation failed");
    }

    /// <summary>Verifies unexpected failures do not expose exception details.</summary>
    [TestMethod]
    public async Task HidesUnexpectedFailureDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.unexpected",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "failure" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().BeTrue();
        var text = ((TextContentBlock)result.Content[0]).Text;
        text.Should().NotContain("synthetic handler failed");
        result.StructuredContent!.Value.GetProperty("status").GetInt32().Should().Be(500);
    }

    /// <summary>Verifies MCP attachment uploads are converted before processor dispatch.</summary>
    [TestMethod]
    public async Task UploadsAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.attachment.upload",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["attachment"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = "hello.txt",
                    ["mimeType"] = "text/plain",
                    ["blob"] = Convert.ToBase64String("hello attachment"u8.ToArray()),
                },
            },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().NotBe(true);
        fixture.State.LastAttachmentName.Should().Be("hello.txt");
        fixture.State.LastAttachmentContent.Should().Be("hello attachment");
    }

    /// <summary>Verifies MCP attachment downloads are returned as embedded resources.</summary>
    [TestMethod]
    public async Task DownloadsAttachment()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.attachment.download",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "download.txt" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().NotBe(true);
        result.Content.Should().ContainSingle();
        var resource = ((EmbeddedResourceBlock)result.Content[0]).Resource;
        resource.Should().BeOfType<BlobResourceContents>();
        var blob = (BlobResourceContents)resource;
        blob.Blob.Span.SequenceEqual("downloaded content"u8.ToArray()).Should().BeTrue();
        blob.MimeType.Should().Be("text/plain");
    }

    /// <summary>Verifies each MCP version exposes only contracts active in that version.</summary>
    [TestMethod]
    public async Task ListsToolsForContractVersion()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var versionOne = await _createClientAsync(app, version: "1").ConfigureAwait(false);
        await using var versionTwo = await _createClientAsync(app, version: "2").ConfigureAwait(false);
        await using var versionFour = await _createClientAsync(app, version: "4").ConfigureAwait(false);

        var versionOneTools = await versionOne.ListToolsAsync(
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var versionTwoTools = await versionTwo.ListToolsAsync(
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);
        var versionFourTools = await versionFour.ListToolsAsync(
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        versionOneTools.Select(static tool => tool.Name).Should().NotContain("hosting.versioned");
        versionTwoTools.Select(static tool => tool.Name).Should().Contain("hosting.versioned");
        versionFourTools.Select(static tool => tool.Name).Should().NotContain("hosting.versioned");
        versionFourTools.Select(static tool => tool.Name).Should().Contain("hosting.query");
    }

    private static async Task<McpClient> _createClientAsync(
        WebApplication app,
        string? token = null,
        string version = "1")
    {
        var httpClient = app.GetTestServer().CreateClient();
        if (token is not null)
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

#pragma warning disable CA2000
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost/mcp/v" + version),
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            httpClient,
            NullLoggerFactory.Instance,
            true);
#pragma warning restore CA2000
        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions(),
            NullLoggerFactory.Instance,
            app.Lifetime.ApplicationStopping).ConfigureAwait(false);
    }
#pragma warning restore MA0004
}
