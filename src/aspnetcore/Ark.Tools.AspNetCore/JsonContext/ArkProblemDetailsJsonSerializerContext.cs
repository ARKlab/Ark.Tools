// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.AspNetCore.ProblemDetails;

using System.Text.Json.Serialization;

namespace Ark.Tools.AspNetCore.JsonContext;

/// <summary>
/// JSON serialization source generation context for Ark ProblemDetails types.
/// This context includes all common error response types used by Ark.Tools middleware.
/// Configured with Ark default settings at compile-time via JsonSourceGenerationOptions attribute.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "JSON source generation for Exception types requires TargetSite property which uses reflection. This is acceptable for error serialization scenarios.")]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(FluentValidationProblemDetails), TypeInfoPropertyName = "FluentValidationProblemDetails")]
[JsonSerializable(typeof(FluentValidationErrors), TypeInfoPropertyName = "FluentValidationErrors")]
[JsonSerializable(typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), TypeInfoPropertyName = "ProblemDetails")]
[JsonSerializable(typeof(Dictionary<string, FluentValidationErrors[]>), TypeInfoPropertyName = "DictionaryStringFluentValidationErrors")]
[JsonSerializable(typeof(Dictionary<string, object>), TypeInfoPropertyName = "DictionaryStringObject")]
public sealed partial class ArkProblemDetailsJsonSerializerContext : JsonSerializerContext
{
}