// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Provides the JSON options used by generated MCP tools.</summary>
public static partial class McpToolSerializerOptions
{
    /// <summary>
    /// Gets the JSON options for generated MCP tool arguments, schemas and results: a read-only copy of the
    /// Minimal API options (<c>ConfigureHttpJsonOptions</c>) with the MCP protocol contracts and
    /// <see cref="McpAttachmentInput"/> appended to the resolver chain.
    /// </summary>
    /// <param name="services">The service provider of the MCP host.</param>
    /// <returns>The JSON options shared by the generated tools.</returns>
    /// <remarks>
    /// The appended resolvers come after the application's own resolvers, so they only supply metadata the
    /// application does not provide. They make attachment tools work when reflection-based serialization is
    /// disabled, for example under Native AOT.
    /// </remarks>
    public static JsonSerializerOptions Get(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetService<Holder>()?.Options ?? _create(services);
    }

    private static JsonSerializerOptions _create(IServiceProvider services)
    {
        var options = new JsonSerializerOptions(
            services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions);
        options.TypeInfoResolverChain.Add(McpJsonUtilities.DefaultOptions.TypeInfoResolver!);
        options.TypeInfoResolverChain.Add(McpToolJsonSerializerContext.Default);
        options.MakeReadOnly();
        return options;
    }

    internal sealed class Holder(IServiceProvider services)
    {
        public JsonSerializerOptions Options { get; } = _create(services);
    }

    [JsonSourceGenerationOptions(
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(McpAttachmentInput))]
    internal sealed partial class McpToolJsonSerializerContext : JsonSerializerContext
    {
    }
}
