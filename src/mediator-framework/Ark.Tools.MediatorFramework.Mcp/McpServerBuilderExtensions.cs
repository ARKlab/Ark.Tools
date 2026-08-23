// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

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
    public static IMcpServerBuilder WithArkMcpTools<
        [global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
            global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods)] TContext>(
        this IMcpServerBuilder builder)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        return TContextRegistration<TContext>.Register(builder);
    }

    private static class TContextRegistration<
        [global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods |
            global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods)] TContext>
        where TContext : class
    {
        public static IMcpServerBuilder Register(IMcpServerBuilder builder)
        {
            var method = typeof(TContext).GetMethod(
                "RegisterMcpTools",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return method?.Invoke(null, [builder]) as IMcpServerBuilder ?? builder;
        }
    }
}
