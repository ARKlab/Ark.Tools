// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher;

/// <summary>
/// JSON serialization source generation context for SqlStateProvider internal fields.
/// This context is used for serializing internal state fields (ModifiedSources) with Ark defaults.
/// Configured with Ark default settings at compile-time via JsonSourceGenerationOptions attribute.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Dictionary<string, LocalDateTime>))]
internal sealed partial class SqlStateProviderJsonContext : JsonSerializerContext
{
}
