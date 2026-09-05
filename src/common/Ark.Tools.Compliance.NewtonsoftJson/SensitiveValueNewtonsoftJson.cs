// Copyright (C) 2026 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Newtonsoft.Json;

namespace Ark.Tools.Compliance.NewtonsoftJson;

/// <summary>
/// Writes a sensitive value object as its cleartext transport value and rehydrates it.
/// </summary>
/// <typeparam name="T">The sensitive value object.</typeparam>
public sealed class SensitiveValueJsonConverter<T> : JsonConverter<T>
    where T : struct, ISensitiveValue<T>
{
    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteValue(SensitiveValueSerialization.ToTransport(value, "NewtonsoftJson"));
    }

    /// <inheritdoc />
    public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (reader.TokenType == JsonToken.Null)
            return default;

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
