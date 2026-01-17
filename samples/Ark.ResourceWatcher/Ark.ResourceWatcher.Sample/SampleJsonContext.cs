// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Dto;
using Ark.Tools.ResourceWatcher;

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
/// <b>How to use with SqlStateProvider:</b>
/// </para>
/// <code>
/// // Create config with JSON context
/// var config = new SqlStateProviderConfigWithContext
/// {
///     DbConnectionString = connectionString,
///     ExtensionsJsonContext = SampleJsonContext.Default
/// };
/// 
/// // Register state provider with typed extensions
/// services.AddSingleton&lt;IStateProvider&lt;MyExtensions&gt;&gt;(sp =&gt;
///     new SqlStateProvider&lt;MyExtensions&gt;(config, connectionManager));
/// </code>
/// <para>
/// The <see cref="JsonSerializableAttribute"/> tells the compiler which types need
/// JSON serialization support. At build time, the compiler generates efficient
/// serialization code for these types.
/// </para>
/// </remarks>
[JsonSerializable(typeof(MyExtensions))]
[JsonSerializable(typeof(ResourceState<MyExtensions>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public sealed partial class SampleJsonContext : JsonSerializerContext
{
    // This class is partial - the compiler generates the implementation at build time
    // The generated code provides:
    // - Metadata for serializing MyExtensions
    // - Metadata for serializing ResourceState<MyExtensions>
    // - Optimized serialization/deserialization methods
    // - No runtime reflection needed
}
