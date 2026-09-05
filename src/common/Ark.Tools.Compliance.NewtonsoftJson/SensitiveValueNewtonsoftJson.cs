// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Newtonsoft.Json;

namespace Ark.Tools.Compliance.NewtonsoftJson;

/// <summary>
/// Writes a sensitive value object as its cleartext transport value and rehydrates it.
/// </summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
/// <remarks>
/// Newtonsoft.Json hands the <c>null</c> token to the converter of a nullable member instead
/// of short-circuiting it as <c>System.Text.Json</c> does, so the converter is untyped: only a
/// <see cref="Nullable{T}"/> target may answer <see langword="null"/>, and a <c>null</c> for a
/// non-nullable member is rejected rather than silently rehydrated as an empty value.
/// </remarks>
public sealed class SensitiveValueJsonConverter<T> : JsonConverter
    where T : struct, ISensitiveValue<T>
{
    /// <inheritdoc />
    public override bool CanConvert(Type objectType) => objectType == typeof(T) || objectType == typeof(T?);

    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(SensitiveValueSerialization.ToTransport((T)value, "NewtonsoftJson"));
    }

    /// <inheritdoc />
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.TokenType == JsonToken.Null)
        {
            return Nullable.GetUnderlyingType(objectType) is not null
                ? null
                : throw new JsonSerializationException($"Cannot convert null to '{typeof(T)}'.");
        }

        if (reader.Value is not string text || !T.TryFrom(text, out var result))
            throw new JsonSerializationException("Invalid sensitive value.");

        return result;
    }
}

/// <summary>
/// Registers sensitive value objects with Newtonsoft.Json.
/// </summary>
public static class SensitiveValueNewtonsoftJson
{
    /// <summary>
    /// Adds the converter for a sensitive value object to the serializer settings.
    /// </summary>
    /// <typeparam name="T">The sensitive value object.</typeparam>
    /// <param name="settings">The serializer settings to configure.</param>
    /// <returns>The same <paramref name="settings"/> for chaining.</returns>
    public static JsonSerializerSettings Register<T>(JsonSerializerSettings settings)
        where T : struct, ISensitiveValue<T>
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Converters.Add(new SensitiveValueJsonConverter<T>());
        return settings;
    }

    /// <summary>
    /// Adds the converters for the sensitive value objects shipped with
    /// <c>Ark.Tools.Compliance</c>.
    /// </summary>
    /// <param name="settings">The serializer settings to configure.</param>
    /// <returns>The same <paramref name="settings"/> for chaining.</returns>
    public static JsonSerializerSettings RegisterBuiltIn(JsonSerializerSettings settings)
    {
        Register<EmailAddress>(settings);
        Register<PhoneNumber>(settings);
        Register<PersonName>(settings);
        Register<PostalAddressLine>(settings);
        Register<NationalIdentifier>(settings);
        Register<ApiKey>(settings);
        return settings;
    }
}
