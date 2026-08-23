// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;

using Microsoft.Extensions.Options;

using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

using System.Linq;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Filters generated MCP tools according to the version route value.</summary>
internal sealed class McpToolVersionSessionFilter : IPostConfigureOptions<HttpServerTransportOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, HttpServerTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var configureSessionOptions = options.ConfigureSessionOptions;
        options.ConfigureSessionOptions = async (httpContext, serverOptions, cancellationToken) =>
        {
            if (configureSessionOptions is not null)
                await configureSessionOptions(httpContext, serverOptions, cancellationToken).ConfigureAwait(false);

            if (!httpContext.Request.RouteValues.TryGetValue("version", out var rawVersion))
                return;

            var routeVersion = Convert.ToString(rawVersion, System.Globalization.CultureInfo.InvariantCulture);
            if (routeVersion?.StartsWith("v", StringComparison.OrdinalIgnoreCase) == true)
                routeVersion = routeVersion[1..];

            if (!int.TryParse(
                    routeVersion,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var version))
            {
                serverOptions.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(
                    StringComparer.OrdinalIgnoreCase);
                return;
            }

            var tools = serverOptions.ToolCollection;
            if (tools is null || tools.Count == 0)
                return;

            var filtered = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase);
            foreach (var tool in tools)
            {
                var versioning = tool.Metadata.OfType<VersioningAttribute>().FirstOrDefault();
                var introduced = versioning?.Introduced ?? 1;
                var retired = versioning?.Retired ?? 0;
                if (version >= introduced && (retired == 0 || version < retired))
                    filtered.Add(tool);
            }

            serverOptions.ToolCollection = filtered;
        };
    }
}
