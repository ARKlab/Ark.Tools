// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// JSON serialization source generation context for SqlStateProvider.
/// This context enables trim-safe serialization of state data to SQL Server.
/// Configured with Ark default settings at compile-time via JsonSourceGenerationOptions attribute.
/// </summary>
/// <remarks>
/// Note: This context is currently not used directly with JsonTypeInfo overloads because
/// we rely on NodaTime converters that must be added via JsonSerializerOptions.
/// The context is kept for documentation and potential future use when NodaTime
/// provides source-generated converters.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(Dictionary<string, LocalDateTime>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal sealed partial class SqlStateProviderJsonContext : JsonSerializerContext
{
}
