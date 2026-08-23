// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Options;

using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Filters generated MCP tools according to the version route value.</summary>
internal sealed class McpToolVersionSessionFilter : IPostConfigureOptions<HttpServerTransportOptions>
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
        HttpServerTransportOptions,
        object> _configuredOptions = [];
    private static readonly System.Threading.Lock _configuredOptionsLock = new();

    /// <inheritdoc />
    public void PostConfigure(string? name, HttpServerTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (_configuredOptionsLock)
        {
            if (_configuredOptions.TryGetValue(options, out _))
                return;

            _configuredOptions.Add(options, new object());
        }

        var configureSessionOptions = options.ConfigureSessionOptions;
        options.ConfigureSessionOptions = async (httpContext, serverOptions, cancellationToken) =>
        {
            if (configureSessionOptions is not null)
                await configureSessionOptions(httpContext, serverOptions, cancellationToken).ConfigureAwait(false);

            if (!httpContext.Request.RouteValues.TryGetValue("version", out var rawVersion))
                return;

            var routeVersion = Convert.ToString(rawVersion, System.Globalization.CultureInfo.InvariantCulture);
            if (routeVersion?.Length > 0 && (routeVersion[0] is 'v' or 'V'))
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
                var versioning = tool.Metadata
                    .OfType<global::Ark.Tools.MediatorFramework.VersioningAttribute>()
                    .FirstOrDefault();
                var introduced = versioning?.Introduced ?? 1;
                var retired = versioning?.Retired ?? 0;
                if (version >= introduced && (retired == 0 || version < retired))
                    filtered.Add(tool);
            }

            serverOptions.ToolCollection = filtered;
        };
    }
}
