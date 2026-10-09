// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.Tools.Nodatime.SystemTextJson.Converters;

using NodaTime;

using System.Text.Json.Serialization;

namespace Ark.Tools.ResourceWatcher;

// Matches the ConfigureArkDefaults output for this type (camelCase keys, ISO LocalDateTime) so stored rows stay readable.
[JsonSourceGenerationOptions(
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
    AllowTrailingCommas = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    Converters = [typeof(LocalDateTimeConverter)])]
[JsonSerializable(typeof(Dictionary<string, LocalDateTime>), TypeInfoPropertyName = "ModifiedSources")]
internal sealed partial class ModifiedSourcesJsonContext : JsonSerializerContext
{
}
