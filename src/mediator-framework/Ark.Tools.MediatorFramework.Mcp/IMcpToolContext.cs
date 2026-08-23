// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Defines the generated registration contract for an MCP tool context.</summary>
public interface IMcpToolContext
{
    /// <summary>Registers the tools generated for this context.</summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The supplied builder.</returns>
    IMcpServerBuilder RegisterMcpTools(IMcpServerBuilder builder);
}
