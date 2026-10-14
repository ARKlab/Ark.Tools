// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

using System.Text.Json;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Provides runtime registration helpers for generated MCP tools.</summary>
public static class McpServerBuilderExtensions
{
    /// <summary>Registers one generated tool with the official MCP server builder.</summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="tool">The generated tool.</param>
    /// <returns>The supplied builder.</returns>
    public static IMcpServerBuilder WithTool(this IMcpServerBuilder builder, McpServerTool tool)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(tool);
        return builder.WithTools([tool]);
    }

    /// <summary>Registers tools generated for <typeparamref name="TContext"/>.</summary>
    /// <typeparam name="TContext">The generated MCP context type.</typeparam>
    /// <param name="builder">The MCP server builder.</param>
    /// <returns>The supplied builder.</returns>
    public static IMcpServerBuilder WithArkMcpTools<TContext>(
        this IMcpServerBuilder builder)
        where TContext : IMcpToolContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        return _addArkMcpFilters(TContext.RegisterMcpTools(builder));
    }

    /// <summary>
    /// Registers tools generated for <typeparamref name="TContext"/>, serializing tool arguments and
    /// structured results with <paramref name="serializerOptions"/> instead of the MCP SDK defaults.
    /// </summary>
    /// <typeparam name="TContext">The generated MCP context type.</typeparam>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="serializerOptions">The JSON options, typically the ones the HTTP endpoints use, so MCP and HTTP return the same shape.</param>
    /// <returns>The supplied builder.</returns>
    public static IMcpServerBuilder WithArkMcpTools<TContext>(
        this IMcpServerBuilder builder,
        JsonSerializerOptions serializerOptions)
        where TContext : IMcpToolContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        return _addArkMcpFilters(TContext.RegisterMcpTools(builder, serializerOptions));
    }

    private static IMcpServerBuilder _addArkMcpFilters(IMcpServerBuilder result)
    {
        result.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IPostConfigureOptions<ModelContextProtocol.AspNetCore.HttpServerTransportOptions>,
                McpToolVersionSessionFilter>());
        return result
            .AddAuthorizationFilters()
            .WithRequestFilters(static filters => filters.AddCallToolFilter(McpToolErrors.CreateFilter()));
    }
}
