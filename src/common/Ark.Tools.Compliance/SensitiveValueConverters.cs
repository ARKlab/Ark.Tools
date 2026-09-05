// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.Compliance;

/// <summary>
/// Writes a sensitive value object as its cleartext transport value and rehydrates it.
/// </summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
public sealed class SensitiveValueJsonConverter<T> : JsonConverter<T>
    where T : struct, ISensitiveValue<T>
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
            throw new JsonException("Invalid sensitive value.");

        if (!T.TryFrom(reader.GetString(), out var result))
            throw new JsonException("Invalid sensitive value.");

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(SensitiveValueSerialization.ToTransport(value, "SystemTextJson"));
    }

    /// <inheritdoc />
    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(SensitiveValueSerialization.ToTransport(value, "SystemTextJson"));
    }
}

/// <summary>
/// Converts a sensitive value object for model binding; conversions to
/// <see cref="string"/> yield the redacted rendering.
/// </summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
public sealed class SensitiveValueTypeConverter<T> : TypeConverter
    where T : struct, ISensitiveValue<T>
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        return value is string text ? T.From(text) : base.ConvertFrom(context, culture, value);
    }

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    {
        return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    }

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return destinationType == typeof(string) && value is T sensitive
            ? sensitive.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
    }
}
