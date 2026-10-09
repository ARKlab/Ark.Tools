// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;

using System.Net.Http.Headers;

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
        var httpClient = host.App.GetTestServer().CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            WebInterfaceTestHost.CreateBearer(ApplicationScopes.BookRead));
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
        var client = await McpClient.CreateAsync(transport, new McpClientOptions(), NullLoggerFactory.Instance, ctk)
            .ConfigureAwait(false);
        await using var __client = client.ConfigureAwait(false);

        var tools = await client.ListToolsAsync(cancellationToken: ctk).ConfigureAwait(false);

        tools.Select(static tool => tool.Name).Should().Contain("books.get");
    }
}
