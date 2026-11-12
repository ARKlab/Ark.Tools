// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using ModelContextProtocol;

using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.MediatorFramework.Mcp;

/// <summary>Provides the JSON options used by generated MCP tools.</summary>
public static partial class McpToolSerializerOptions
{
    // Keyed by the application's options instance, a singleton, so a request-scoped provider gets the same copy.
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _cache = new();
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _readableCache = new();

    /// <summary>
    /// Gets the JSON options for generated MCP tool arguments, schemas and results: a read-only copy of the
    /// Minimal API options (<c>ConfigureHttpJsonOptions</c>) with the MCP protocol contracts and
    /// <see cref="McpAttachmentInput"/> appended to the resolver chain.
    /// </summary>
    /// <param name="services">The service provider of the MCP host.</param>
    /// <returns>The JSON options shared by the generated tools; repeated calls for one application, from any scope, return the same instance.</returns>
    /// <remarks>
    /// The appended resolvers come after the application's own resolvers, so they only supply metadata the
    /// application does not provide. They make attachment tools work when reflection-based serialization is
    /// disabled, for example under Native AOT.
    /// </remarks>
    public static JsonSerializerOptions Get(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return _cache.GetValue(
            services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions,
            static application => _create(application));
    }

    // The same options with an encoder for text that a person reads: accents, apostrophes and markup characters
    // are not escaped; quotes, backslashes and control characters such as line breaks stay escaped.
    internal static JsonSerializerOptions _getReadable(IServiceProvider services)
    {
        return _readableCache.GetValue(Get(services), static options =>
        {
            var readable = new JsonSerializerOptions(options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            readable.MakeReadOnly();
            return readable;
        });
    }

    private static JsonSerializerOptions _create(JsonSerializerOptions application)
    {
        // One line: the error text ends with the details as JSON, and the confirmation question with the arguments.
        var options = new JsonSerializerOptions(application) { WriteIndented = false };
        options.TypeInfoResolverChain.Add(McpJsonUtilities.DefaultOptions.TypeInfoResolver!);
        options.TypeInfoResolverChain.Add(McpToolJsonSerializerContext.Default);
        // ProblemDetails metadata, so these options do not throw NotSupportedException without reflection.
        // For error results, ArkProblemDetailsJson.GetOptions(services) stays the one to pass to McpToolErrors.
        options.TypeInfoResolverChain.Add(Ark.Tools.AspNetCore.ProblemDetails.ArkProblemDetailsJson.GetOptions(services: null).TypeInfoResolver!);
        options.MakeReadOnly();
        return options;
    }

    [JsonSourceGenerationOptions(
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(McpAttachmentInput))]
    internal sealed partial class McpToolJsonSerializerContext : JsonSerializerContext
    {
    }
}
