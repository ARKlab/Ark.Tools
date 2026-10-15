// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.MediatorFramework.Mcp;

using AwesomeAssertions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

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

    /// <summary>
    /// Verifies the tool JSON options resolve the MCP and attachment contracts when the application options
    /// have no reflection fallback, keeping the application resolver first and its options unchanged.
    /// </summary>
    [TestMethod]
    public void ToolSerializerOptionsAppendMcpContractsAfterApplicationResolvers()
    {
        var applicationResolver = new ResolvesNothing();
        var services = new ServiceCollection();
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolver = applicationResolver);
        using var provider = services.BuildServiceProvider();
        var applicationOptions = provider.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        applicationOptions.Invoking(static options => options.GetTypeInfo(typeof(McpAttachmentInput)))
            .Should().Throw<NotSupportedException>();

        var toolOptions = McpToolSerializerOptions.Get(provider);

        toolOptions.IsReadOnly.Should().BeTrue();
        McpToolSerializerOptions.Get(provider).Should().BeSameAs(toolOptions);
        toolOptions.TypeInfoResolverChain[0].Should().BeSameAs(applicationResolver);
        toolOptions.GetTypeInfo(typeof(McpAttachmentInput)).Should().NotBeNull();
        toolOptions.GetTypeInfo(typeof(EmbeddedResourceBlock)).Should().NotBeNull();
        applicationOptions.TypeInfoResolverChain.Should().ContainSingle();
    }

    /// <summary>Verifies a tool result is serialized with the Minimal API JSON options, so it has the HTTP response shape.</summary>
    [TestMethod]
    public async Task ToolResultUsesMinimalApiJsonOptions()
    {
        // Each fixture container backs a single host, so HTTP and MCP get one each.
        await using var httpFixture = new HostingTestFixture();
        await using var fixture = new HostingTestFixture();
        await using var httpApp = httpFixture.BuildMinimalApiHost();
        var jsonOptions = httpApp.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        // A non-default naming policy, so the MCP SDK defaults cannot produce the same shape by chance.
        jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        await httpApp.StartAsync(CancellationToken.None).ConfigureAwait(false);
        using var httpClient = httpApp.GetTestServer().CreateClient();
        var httpResponse = JsonNode.Parse(await httpClient.GetStringAsync(
            new Uri("/api/v1/hosting/queries/7?Value=shared", UriKind.Relative),
            httpApp.Lifetime.ApplicationStopping).ConfigureAwait(false));
        await using var app = fixture.BuildMcpHost(static services => services.ConfigureHttpJsonOptions(
            static options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower));
        await app.StartAsync(CancellationToken.None).ConfigureAwait(false);
        await using var client = await _createClientAsync(app, version: "1").ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.query",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 7, ["value"] = "shared" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().NotBe(true);
        result.StructuredContent.Should().NotBeNull();
        result.StructuredContent!.Value.GetProperty("server_stamp").GetString().Should().Be("hosting-server");
        var mcpResult = JsonNode.Parse(result.StructuredContent.Value.GetRawText());
        JsonNode.DeepEquals(mcpResult, httpResponse).Should().BeTrue(
            "MCP returned {0} while HTTP returned {1}",
            mcpResult?.ToJsonString(),
            httpResponse?.ToJsonString());
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
        blob.DecodedData.Span.SequenceEqual("downloaded content"u8.ToArray()).Should().BeTrue();
        blob.Blob.Span.SequenceEqual("ZG93bmxvYWRlZCBjb250ZW50"u8.ToArray()).Should().BeTrue();
        blob.MimeType.Should().Be("text/plain");
    }

    /// <summary>Verifies an oversized base64 upload is rejected with the size-limit error.</summary>
    [TestMethod]
    public void RejectsOversizedUpload()
    {
        var input = new McpAttachmentInput
        {
            Name = "big.bin",
            Blob = Convert.ToBase64String(new byte[11]),
        };

        input.Invoking(static i => i.ToAttachment(maximumBytes: 10))
            .Should().Throw<FluentValidation.ValidationException>()
            .Which.Errors.Should().ContainSingle(static e => e.ErrorMessage == "The attachment exceeds the configured size limit.");
    }

    /// <summary>Verifies a base64 upload at the limit with line breaks is decoded as before.</summary>
    [TestMethod]
    public async Task DecodesUploadAtLimitWithLineBreaks()
    {
        var input = new McpAttachmentInput
        {
            Name = "hello.txt",
            Blob = "aGVsbG8g\r\nd29ybGQ=",
        };

        var attachment = input.ToAttachment(maximumBytes: 11);

        var stream = attachment.OpenRead();
        await using var _ = stream.ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync().ConfigureAwait(false)).Should().Be("hello world");
    }

    /// <summary>Verifies invalid base64 is rejected with the format error.</summary>
    [TestMethod]
    public void RejectsInvalidBase64Upload()
    {
        var input = new McpAttachmentInput { Name = "bad.bin", Blob = "not base64!" };

        input.Invoking(static i => i.ToAttachment())
            .Should().Throw<FluentValidation.ValidationException>()
            .Which.Errors.Should().ContainSingle(static e => e.ErrorMessage == "Attachment blob must be base64.");
    }

    /// <summary>Verifies downloads from seekable and unseekable streams honour the limit.</summary>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DownloadHonoursLimit(bool seekable)
    {
        var content = "downloaded content"u8.ToArray();
        var attachment = new ArkAttachment("d.txt", "text/plain", () => new TestStream(content, seekable));

        var block = await McpAttachmentResults.ToEmbeddedResourceAsync(attachment, maximumBytes: content.Length)
            .ConfigureAwait(false);
        ((BlobResourceContents)block.Resource).DecodedData.ToArray().Should().Equal(content);

        await FluentActions.Awaiting(() => McpAttachmentResults.ToEmbeddedResourceAsync(attachment, maximumBytes: content.Length - 1))
            .Should().ThrowAsync<FluentValidation.ValidationException>().ConfigureAwait(false);
    }

    /// <summary>Verifies a seekable stream reporting zero length is still read, within the limit.</summary>
    [TestMethod]
    public async Task DownloadReadsSeekableStreamReportingZeroLength()
    {
        var content = "proc-like content"u8.ToArray();
        var attachment = new ArkAttachment("p.txt", "text/plain", () => new ZeroLengthStream(content));

        var block = await McpAttachmentResults.ToEmbeddedResourceAsync(attachment, maximumBytes: content.Length)
            .ConfigureAwait(false);
        ((BlobResourceContents)block.Resource).DecodedData.ToArray().Should().Equal(content);

        await FluentActions.Awaiting(() => McpAttachmentResults.ToEmbeddedResourceAsync(attachment, maximumBytes: content.Length - 1))
            .Should().ThrowAsync<FluentValidation.ValidationException>().ConfigureAwait(false);
    }

    /// <summary>Verifies a seekable stream longer than it reports is read in full and still checked against the limit.</summary>
    [TestMethod]
    public async Task DownloadReadsSeekableStreamGrowingBeyondReportedLength()
    {
        var content = "growing!"u8.ToArray();
        var attachment = new ArkAttachment("g.txt", "text/plain", () => new ShortLengthStream(content, content.Length / 2));

        var block = await McpAttachmentResults.ToEmbeddedResourceAsync(attachment, maximumBytes: content.Length)
            .ConfigureAwait(false);
        ((BlobResourceContents)block.Resource).DecodedData.ToArray().Should().Equal(content);

        await FluentActions.Awaiting(() => McpAttachmentResults.ToEmbeddedResourceAsync(attachment, maximumBytes: content.Length - 2))
            .Should().ThrowAsync<FluentValidation.ValidationException>().ConfigureAwait(false);
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
        [NotPersonalData("Synthetic test bearer token.")] string? token = null,
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

    private sealed class TestStream(byte[] content, bool seekable) : MemoryStream(content, writable: false)
    {
        public override bool CanSeek => seekable;
    }

    private sealed class ZeroLengthStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override long Length => 0;
    }

    private sealed class ShortLengthStream(byte[] content, long reportedLength) : MemoryStream(content, writable: false)
    {
        public override long Length => reportedLength;
    }

    private sealed class ResolvesNothing : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            return null;
        }
    }
}
