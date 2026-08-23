// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Describes the generated MCP tools available in each API version.</summary>
public interface IMcpToolVersionMap
{
    /// <summary>Determines whether a tool is available in an API version.</summary>
    /// <param name="toolName">The MCP tool name.</param>
    /// <param name="version">The API version.</param>
    /// <returns><see langword="true"/> when the tool is available; otherwise, <see langword="false"/>.</returns>
    bool IsToolActive(string toolName, int version);
}

/// <summary>Provides efficient lookup of generated MCP tools by API version.</summary>
public sealed class McpToolVersionMap : IMcpToolVersionMap
{
    private readonly Dictionary<int, HashSet<string>> _toolsByVersion = [];

    /// <summary>Initializes a new instance of the <see cref="McpToolVersionMap"/> class.</summary>
    /// <param name="toolsByVersion">The generated tool names grouped by API version.</param>
    public McpToolVersionMap(IReadOnlyDictionary<int, string[]> toolsByVersion)
    {
        ArgumentNullException.ThrowIfNull(toolsByVersion);

        foreach (var pair in toolsByVersion)
        {
            ArgumentNullException.ThrowIfNull(pair.Value);
            _toolsByVersion.Add(
                pair.Key,
                new HashSet<string>(pair.Value, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <inheritdoc />
    public bool IsToolActive(string toolName, int version)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        return _toolsByVersion.TryGetValue(version, out var tools) && tools.Contains(toolName);
    }
}
