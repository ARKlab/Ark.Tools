// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;
using Ark.ResourceWatcher.Sample.Provider;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.ResourceWatcher.Sample;

/// <summary>
/// JSON serialization context for the ResourceWatcher sample.
/// Provides source-generated JSON serialization for Native AoT compatibility.
/// </summary>
/// <remarks>
/// <para>
/// This context enables Native AoT (Ahead-of-Time) compilation by providing compile-time
/// generated JSON serialization code instead of relying on runtime reflection.
/// </para>
/// <para>
/// <b>When to use source-generated JSON contexts:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Publishing with Native AoT (<c>PublishAot=true</c>)</description></item>
/// <item><description>Publishing with trimming (<c>PublishTrimmed=true</c>)</description></item>
/// <item><description>To improve startup performance (no reflection metadata generation)</description></item>
/// <item><description>To reduce memory footprint (smaller metadata)</description></item>
/// </list>
/// <para>
/// The sample's HTTP clients use this context through <see cref="SampleJson.HttpOptions"/>.
/// </para>
/// <para>
/// <b>How to use with SqlStateProvider</b> (needs the <c>Ark.Tools.ResourceWatcher.WorkerHost.Sql</c> package):
/// </para>
/// <code>
/// // Config that hands the context to the SqlStateProvider
/// public sealed class MySqlStateProviderConfig : ISqlStateProviderConfig
/// {
///     public required string DbConnectionString { get; init; }
///     public JsonSerializerContext? ExtensionsJsonContext => SampleJsonContext.Default;
/// }
///
/// // In the MyWorkerHost constructor, instead of UseStateProvider&lt;InMemStateProvider&lt;MyExtensions&gt;&gt;()
/// this.UseSqlStateProvider(new MySqlStateProviderConfig { DbConnectionString = connectionString });
/// </code>
/// <para>
/// The <see cref="JsonSerializableAttribute"/> tells the compiler which types need
/// JSON serialization support. At build time, the compiler generates efficient
/// serialization code for these types.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(MyExtensions))]
[JsonSerializable(typeof(MyListResponse))]
[JsonSerializable(typeof(SinkDto))]
internal sealed partial class SampleJsonContext : JsonSerializerContext
{
    // This class is partial - the compiler generates the implementation at build time
    // The generated code provides:
    // - Metadata for serializing MyExtensions and the HTTP DTOs
    // - Optimized serialization/deserialization methods
    // - No runtime reflection needed
}

/// <summary>
/// JSON options for the sample's HTTP clients.
/// </summary>
internal static class SampleJson
{
    /// <summary>
    /// Gets the Ark default options with <see cref="SampleJsonContext"/> as the only metadata source.
    /// </summary>
    /// <remarks>
    /// A type missing from <see cref="SampleJsonContext"/> fails instead of falling back to reflection.
    /// </remarks>
    public static JsonSerializerOptions HttpOptions { get; } = new JsonSerializerOptions(ArkSerializerOptions.JsonOptions)
    {
        TypeInfoResolver = SampleJsonContext.Default
    };
}
