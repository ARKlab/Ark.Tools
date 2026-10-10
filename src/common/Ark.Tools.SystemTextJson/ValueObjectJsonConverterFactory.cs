// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Ark.Tools.SystemTextJson;

/// <summary>
/// Applies the type-level <see cref="JsonConverterAttribute"/> of Vogen value objects
/// (<c>[ValueObject&lt;T&gt;]</c> or <c>[ValueObject(typeof(T))]</c>).
/// </summary>
/// <remarks>
/// Vogen adds the attribute in generated code, which a <see cref="JsonSerializerContext"/> in the same assembly
/// cannot see: without this factory the source-generated metadata serializes the value object as an empty object.
/// Value objects are detected by attribute name, so this package takes no Vogen dependency.
/// </remarks>
public sealed class ValueObjectJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        return typeToConvert.CustomAttributes.Any(static attribute =>
                attribute.AttributeType.FullName is { } name
                && (name == "Vogen.ValueObjectAttribute" || name.StartsWith("Vogen.ValueObjectAttribute`1", StringComparison.Ordinal)))
            && typeToConvert.GetCustomAttribute<JsonConverterAttribute>() is not null;
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);
        var attribute = typeToConvert.GetCustomAttribute<JsonConverterAttribute>()!;
        var converter = attribute.CreateConverter(typeToConvert)
            ?? (attribute.ConverterType is { } converterType ? (JsonConverter?)Activator.CreateInstance(converterType) : null);
        return converter is JsonConverterFactory factory
            ? factory.CreateConverter(typeToConvert, options)
            : converter;
    }
}
