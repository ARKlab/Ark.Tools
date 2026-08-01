// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using Ark.Tools.Core;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>
/// Default JSON converter factory for <see cref="EvolvableEnum{TEnum}"/>: serializes using the
/// symbolic member name, matching the strict-enum default. Registered automatically by
/// <see cref="Extensions.ConfigureArkDefaults(JsonSerializerOptions)"/>.
/// </summary>
public class EvolvableEnumJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(EvolvableEnum<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => EvolvableEnumJsonConverter.Create(typeToConvert, EvolvableEnumWireFormat.Name);
}

/// <summary>
/// Opt-in JSON converter factory for <see cref="EvolvableEnum{TEnum}"/> that serializes using the
/// numeric underlying value instead of the default symbolic name. Apply explicitly to a property
/// via <c>[JsonConverter(typeof(EvolvableEnumIntegerJsonConverterFactory))]</c> when a contract
/// intentionally stores the numeric wire value in JSON.
/// </summary>
public class EvolvableEnumIntegerJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(EvolvableEnum<>);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => EvolvableEnumJsonConverter.Create(typeToConvert, EvolvableEnumWireFormat.Number);
}

/// <summary>Non-generic helper that constructs the closed generic converter via reflection.</summary>
internal static class EvolvableEnumJsonConverter
{
    [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType", Justification = "enumType is a runtime Enum type extracted from a closed EvolvableEnum<TEnum>; its public fields (the enum members) are always available for a concrete enum type.")]
    public static JsonConverter Create(Type typeToConvert, EvolvableEnumWireFormat format)
    {
        var enumType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(Converter<>).MakeGenericType(enumType);

        return (JsonConverter)Activator.CreateInstance(
            converterType,
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: [format],
            culture: null)!;
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "CA1812", Justification = "Instantiated via reflection in Create method")]
    private sealed class Converter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> : JsonConverter<EvolvableEnum<TEnum>>
        where TEnum : struct, Enum
    {
        private readonly EvolvableEnumWireFormat _format;

        public Converter(EvolvableEnumWireFormat format)
        {
            _format = format;
        }

        public override EvolvableEnum<TEnum> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return EvolvableEnum<TEnum>.FromName(reader.GetString()!);

                case JsonTokenType.Number:
                    return EvolvableEnum<TEnum>.IsUnsignedUnderlyingType
                        ? EvolvableEnum<TEnum>.FromNumber(reader.GetUInt64())
                        : EvolvableEnum<TEnum>.FromNumber(reader.GetInt64());

                default:
                    throw new JsonException($"Cannot deserialize {typeof(EvolvableEnum<TEnum>)}: expected a JSON string or number, found {reader.TokenType}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, EvolvableEnum<TEnum> value, JsonSerializerOptions options)
        {
            if (_format == EvolvableEnumWireFormat.Number)
            {
                WriteNumber(writer, value);
                return;
            }

            var name = value.Name;
            if (name is not null)
            {
                writer.WriteStringValue(name);
                return;
            }

            // The value is an unknown number with no symbolic name (typically produced by an
            // upstream binary transport such as protobuf or MessagePack). There is no safe string
            // representation that preserves it, so fail explicitly instead of corrupting the value.
            throw new EvolvableEnumConversionException(
                $"Cannot serialize '{value}' as a JSON string: the value has no symbolic name. Use {nameof(EvolvableEnumIntegerJsonConverterFactory)} to serialize the numeric value instead.");
        }

        private static void WriteNumber(Utf8JsonWriter writer, EvolvableEnum<TEnum> value)
        {
            if (EvolvableEnum<TEnum>.IsUnsignedUnderlyingType)
                writer.WriteNumberValue(value.ToUInt64());
            else
                writer.WriteNumberValue(value.ToInt64());
        }
    }
}
