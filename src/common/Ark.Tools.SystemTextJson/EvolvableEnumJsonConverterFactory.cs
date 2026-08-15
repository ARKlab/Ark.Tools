// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>Serializes evolvable enums by symbolic name.</summary>
public class EvolvableEnumJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => EvolvableEnumJsonConverter.IsSupported(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => EvolvableEnumJsonConverter.Create(typeToConvert, EvolvableEnumWireFormat.Name);
}

/// <summary>Serializes evolvable enums using their exact numeric backing type.</summary>
public class EvolvableEnumIntegerJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => EvolvableEnumJsonConverter.IsSupported(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => EvolvableEnumJsonConverter.Create(typeToConvert, EvolvableEnumWireFormat.Number);
}

internal static class EvolvableEnumJsonConverter
{
    public static bool IsSupported(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(EvolvableEnum<>) || definition == typeof(EvolvableEnum<,>);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType", Justification = "Arguments come from a closed EvolvableEnum type.")]
    public static JsonConverter Create(Type type, EvolvableEnumWireFormat format)
    {
        var arguments = type.GetGenericArguments();
        var converterDefinition = arguments.Length == 1 ? typeof(DefaultConverter<>) : typeof(Converter<,>);
        var converterType = converterDefinition.MakeGenericType(arguments);
        return (JsonConverter)Activator.CreateInstance(
            converterType,
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: [format],
            culture: null)!;
    }

    private sealed class DefaultConverter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> :
        JsonConverter<EvolvableEnum<TEnum>>
        where TEnum : struct, Enum
    {
        private readonly EvolvableEnumWireFormat _format;

        public DefaultConverter(EvolvableEnumWireFormat format)
        {
            _format = format;
        }

        public override EvolvableEnum<TEnum> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch
            {
                JsonTokenType.String => EvolvableEnum<TEnum>.FromName(reader.GetString()!),
                JsonTokenType.Number => EvolvableEnum<TEnum>.FromNumber(reader.GetInt32()),
                _ => throw _unexpectedToken<TEnum>(reader.TokenType),
            };

        public override void Write(
            Utf8JsonWriter writer,
            EvolvableEnum<TEnum> value,
            JsonSerializerOptions options)
        {
            if (_format == EvolvableEnumWireFormat.Number)
            {
                writer.WriteNumberValue(value.ToNumber());
                return;
            }

            _writeName(writer, value.Name, value);
        }
    }

    private sealed class Converter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
        TBacking> :
        JsonConverter<EvolvableEnum<TEnum, TBacking>>
        where TEnum : struct, Enum
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        private readonly EvolvableEnumWireFormat _format;

        public Converter(EvolvableEnumWireFormat format)
        {
            _format = format;
        }

        public override EvolvableEnum<TEnum, TBacking> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => reader.TokenType switch
            {
                JsonTokenType.String => EvolvableEnum<TEnum, TBacking>.FromName(reader.GetString()!),
                JsonTokenType.Number => EvolvableEnum<TEnum, TBacking>.FromNumber(_readNumber<TBacking>(ref reader)),
                _ => throw _unexpectedToken<TEnum>(reader.TokenType),
            };

        public override void Write(
            Utf8JsonWriter writer,
            EvolvableEnum<TEnum, TBacking> value,
            JsonSerializerOptions options)
        {
            if (_format == EvolvableEnumWireFormat.Number)
            {
                _writeNumber(writer, value.ToNumber());
                return;
            }

            _writeName(writer, value.Name, value);
        }
    }

    private static JsonException _unexpectedToken<TEnum>(JsonTokenType token)
        => new($"Cannot deserialize EvolvableEnum<{typeof(TEnum).Name}> from {token}.");

    private static void _writeName(Utf8JsonWriter writer, string? name, object value)
    {
        if (name is null)
            throw new EvolvableEnumConversionException(
                $"Cannot serialize '{value}' as a JSON string because it has no symbolic name.");

        writer.WriteStringValue(name);
    }

    private static TBacking _readNumber<TBacking>(ref Utf8JsonReader reader)
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        object value = Type.GetTypeCode(typeof(TBacking)) switch
        {
            TypeCode.SByte => checked((sbyte)reader.GetInt32()),
            TypeCode.Byte => checked((byte)reader.GetUInt32()),
            TypeCode.Int16 => checked((short)reader.GetInt32()),
            TypeCode.UInt16 => checked((ushort)reader.GetUInt32()),
            TypeCode.Int32 => reader.GetInt32(),
            TypeCode.UInt32 => reader.GetUInt32(),
            TypeCode.Int64 => reader.GetInt64(),
            TypeCode.UInt64 => reader.GetUInt64(),
            _ => throw new NotSupportedException($"Unsupported evolvable enum backing type {typeof(TBacking)}."),
        };
        return (TBacking)value;
    }

    private static void _writeNumber<TBacking>(Utf8JsonWriter writer, TBacking value)
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        switch (value)
        {
            case sbyte number:
                writer.WriteNumberValue(number);
                break;
            case byte number:
                writer.WriteNumberValue(number);
                break;
            case short number:
                writer.WriteNumberValue(number);
                break;
            case ushort number:
                writer.WriteNumberValue(number);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case uint number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            default:
                throw new NotSupportedException($"Unsupported evolvable enum backing type {typeof(TBacking)}.");
        }
    }
}
