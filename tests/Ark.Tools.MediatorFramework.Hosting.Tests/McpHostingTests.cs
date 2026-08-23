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
        await using var client = await _createClientAsync(app).ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

        tools.Select(tool => tool.Name).Should().BeEquivalentTo(
            ["hosting.query", "hosting.unexpected", "hosting.validation"]);
    }

    /// <summary>Verifies an authenticated MCP client sees the protected generated tool.</summary>
    [TestMethod]
    public async Task ListsProtectedToolForAuthenticatedClient()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app, "authenticated").ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false);

        tools.Select(tool => tool.Name).Should().Contain("hosting.authorized");
    }

    /// <summary>Verifies an anonymous client cannot invoke a protected generated tool.</summary>
    [TestMethod]
    public async Task RejectsProtectedToolForAnonymousClient()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app).ConfigureAwait(false);

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
        await using var client = await _createClientAsync(app).ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.query",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 7, ["value"] = "from-mcp" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        Console.WriteLine(((TextContentBlock)result.Content[0]).Text);
        result.IsError.Should().NotBe(true);
        result.StructuredContent.Should().NotBeNull();
        result.StructuredContent!.Value.GetProperty("Message").GetString().Should().Be("7:from-mcp");
    }

    /// <summary>Verifies validation failures become safe structured MCP errors.</summary>
    [TestMethod]
    public async Task MapsValidationFailureToMcpError()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app).ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.validation",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "invalid" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        Console.WriteLine(((TextContentBlock)result.Content[0]).Text);
        Console.WriteLine(result.StructuredContent?.GetRawText());
        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        ((TextContentBlock)result.Content[0]).Text.Should().Be(
            "Validation failed: One or more validation errors occurred.");
        result.StructuredContent!.Value.GetProperty("title").GetString().Should().Be("Validation failed");
    }

    /// <summary>Verifies unexpected failures do not expose exception details.</summary>
    [TestMethod]
    public async Task HidesUnexpectedFailureDetails()
    {
        await using var fixture = new HostingTestFixture();
        await using var app = await fixture.StartMcpHostAsync().ConfigureAwait(false);
        await using var client = await _createClientAsync(app).ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "hosting.unexpected",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "failure" },
            cancellationToken: app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        result.IsError.Should().BeTrue();
        var text = ((TextContentBlock)result.Content[0]).Text;
        text.Should().NotContain("synthetic handler failed");
        result.StructuredContent!.Value.GetProperty("status").GetInt32().Should().Be(500);
    }

    private static async Task<McpClient> _createClientAsync(
        WebApplication app,
        string? token = null)
    {
        var httpClient = app.GetTestServer().CreateClient();
        if (token is not null)
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

#pragma warning disable CA2000
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost/mcp"),
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
