// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ModelContextProtocol.Protocol;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Creates successful MCP tool results.</summary>
public static class McpToolResults
{
    /// <summary>An empty successful result for commands.</summary>
    public static CallToolResult Empty { get; } = new();

    /// <summary>Creates a successful structured result.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="value">The result value.</param>
    /// <returns>A successful MCP tool result.</returns>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The MCP SDK serializes generated tool result types at the tool boundary.")]
    public static CallToolResult ToToolResult<T>(T value)
    {
        return new CallToolResult
        {
            StructuredContent = System.Text.Json.JsonSerializer.SerializeToElement(value),
        };
    }
}
