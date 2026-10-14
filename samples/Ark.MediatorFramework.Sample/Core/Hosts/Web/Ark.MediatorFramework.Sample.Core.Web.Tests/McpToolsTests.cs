// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json.Nodes;

namespace Ark.MediatorFramework.Sample.Core.Web.Tests;

/// <summary>Covers the generated MCP endpoint of the Web host through the MCP SDK client.</summary>
[TestClass]
public sealed class McpToolsTests
{
    /// <summary>Lists the tools with the integration-test bearer.</summary>
    [TestMethod]
    public async Task McpToolsListIncludesBookTools()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        var client = await _createClientAsync(host, ApplicationScopes.BookRead).ConfigureAwait(false);
        await using var __client = client.ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: ctk).ConfigureAwait(false);

        tools.Select(static tool => tool.Name).Should().Contain("books.get");
    }

    /// <summary>Reads a book over MCP and expects the same JSON shape the HTTP endpoints return.</summary>
    [TestMethod]
    public async Task McpBookResultMatchesHttpShape()
    {
        await using var host = await WebInterfaceTestHost.StartAsync().ConfigureAwait(false);
        var ctk = host.App.Lifetime.ApplicationStopping;
        using var httpClient = host.CreateClient(ApplicationScopes.BookWrite);
        using var books = new StringContent(
            """[{"title":"Dune","author":"Frank Herbert","genre":"Fiction"}]""",
            Encoding.UTF8,
            MediaTypeNames.Application.Json);
        using var create = await httpClient.PostAsync(new Uri("/api/v1/books/bulk", UriKind.Relative), books, ctk)
            .ConfigureAwait(false);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var httpBook = JsonNode.Parse(await create.Content.ReadAsStringAsync(ctk).ConfigureAwait(false))![0]!;
        var client = await _createClientAsync(host, ApplicationScopes.BookRead).ConfigureAwait(false);
        await using var __client = client.ConfigureAwait(false);

        var result = await client.CallToolAsync(
            "books.get",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = httpBook["id"]!.GetValue<string>() },
            cancellationToken: ctk).ConfigureAwait(false);

        result.IsError.Should().NotBe(true);
        result.StructuredContent.Should().NotBeNull();
        var mcpBook = JsonNode.Parse(result.StructuredContent!.Value.GetRawText());
        JsonNode.DeepEquals(mcpBook, httpBook).Should().BeTrue(
            "MCP returned {0} while HTTP returned {1}",
            mcpBook?.ToJsonString(),
            httpBook.ToJsonString());
    }

    private static async Task<McpClient> _createClientAsync(WebInterfaceTestHost host, params string[] scopes)
    {
        var httpClient = host.App.GetTestServer().CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            WebInterfaceTestHost.CreateBearer(scopes));
#pragma warning disable CA2000 // The client owns and disposes the transport, which owns the HTTP client.
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost/mcp/v1"),
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
            host.App.Lifetime.ApplicationStopping).ConfigureAwait(false);
    }
}
