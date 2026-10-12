// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Compliance;
using Ark.Tools.Core;

using System.Collections.Frozen;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>Serializes evolvable enums by symbolic name.</summary>
public sealed class EvolvableEnumJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => EvolvableEnumJsonConverter.IsSupported(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => EvolvableEnumJsonConverter.Create(
            typeToConvert,
            EvolvableEnumWireFormat.Name,
            options.Encoder ?? JavaScriptEncoder.Default);
}

/// <summary>Serializes evolvable enums using their exact numeric backing type.</summary>
public sealed class EvolvableEnumIntegerJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => EvolvableEnumJsonConverter.IsSupported(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => EvolvableEnumJsonConverter.Create(
            typeToConvert,
            EvolvableEnumWireFormat.Number,
            options.Encoder ?? JavaScriptEncoder.Default);
}

/// <summary>
/// Serializes <see cref="EvolvableEnum{TEnum}"/> by symbolic name, without reflection.
/// </summary>
/// <remarks>
/// <see cref="EvolvableEnumJsonConverterFactory"/> closes its converters at run time, which Native AOT cannot do. In an
/// AOT app, list this converter per enum in the source-generated context instead, for example
/// <c>[JsonSourceGenerationOptions(Converters = new[] { typeof(EvolvableEnumJsonConverter&lt;Genre&gt;) })]</c>.
/// </remarks>
/// <typeparam name="TEnum">The wrapped enum.</typeparam>
public sealed class EvolvableEnumJsonConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> :
    JsonConverter<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    private readonly ConditionalWeakTable<JavaScriptEncoder, JsonConverter<EvolvableEnum<TEnum>>> _converters = new();

    /// <inheritdoc />
    public override EvolvableEnum<TEnum> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _getConverter(options).Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, EvolvableEnum<TEnum> value, JsonSerializerOptions options)
    {
        _getConverter(options).Write(writer, value, options);
    }

    // Like the factory, encodes names with the options' encoder. One instance can serve several options, so the
    // inner converter is cached per encoder.
    private JsonConverter<EvolvableEnum<TEnum>> _getConverter(JsonSerializerOptions options)
    {
        return _converters.GetValue(options.Encoder ?? JavaScriptEncoder.Default, EvolvableEnumJsonConverter.CreateByName<TEnum>);
    }
}

/// <summary>
/// Serializes <see cref="EvolvableEnum{TEnum, TBacking}"/> by symbolic name, without reflection.
/// </summary>
/// <remarks>
/// The Native AOT counterpart of <see cref="EvolvableEnumJsonConverterFactory"/> for enums with an explicit backing type;
/// list it per enum in the source-generated context, for example
/// <c>[JsonSourceGenerationOptions(Converters = new[] { typeof(EvolvableEnumJsonConverter&lt;Status, byte&gt;) })]</c>.
/// </remarks>
/// <typeparam name="TEnum">The wrapped enum.</typeparam>
/// <typeparam name="TBacking">The numeric backing type.</typeparam>
public sealed class EvolvableEnumJsonConverter<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
    TBacking> :
    JsonConverter<EvolvableEnum<TEnum, TBacking>>
    where TEnum : struct, Enum
    where TBacking : struct, IBinaryInteger<TBacking>
{
    private readonly ConditionalWeakTable<JavaScriptEncoder, JsonConverter<EvolvableEnum<TEnum, TBacking>>> _converters = new();

    /// <inheritdoc />
    public override EvolvableEnum<TEnum, TBacking> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _getConverter(options).Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, EvolvableEnum<TEnum, TBacking> value, JsonSerializerOptions options)
    {
        _getConverter(options).Write(writer, value, options);
    }

    private JsonConverter<EvolvableEnum<TEnum, TBacking>> _getConverter(JsonSerializerOptions options)
    {
        return _converters.GetValue(options.Encoder ?? JavaScriptEncoder.Default, EvolvableEnumJsonConverter.CreateByName<TEnum, TBacking>);
    }
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

    public static JsonConverter<EvolvableEnum<TEnum>> CreateByName<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(JavaScriptEncoder encoder)
        where TEnum : struct, Enum
        => new DefaultConverter<TEnum>(EvolvableEnumWireFormat.Name, encoder);

    public static JsonConverter<EvolvableEnum<TEnum, TBacking>> CreateByName<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
        TBacking>(JavaScriptEncoder encoder)
        where TEnum : struct, Enum
        where TBacking : struct, IBinaryInteger<TBacking>
        => new Converter<TEnum, TBacking>(EvolvableEnumWireFormat.Name, encoder);

    [UnconditionalSuppressMessage("Trimming", "IL2055:MakeGenericType", Justification = "Arguments come from a closed EvolvableEnum type.")]
    public static JsonConverter Create(Type type, EvolvableEnumWireFormat format, JavaScriptEncoder encoder)
    {
        var arguments = type.GetGenericArguments();
        var converterDefinition = arguments.Length == 1 ? typeof(DefaultConverter<>) : typeof(Converter<,>);
        var converterType = converterDefinition.MakeGenericType(arguments);
        return (JsonConverter)Activator.CreateInstance(
            converterType,
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: [format, encoder],
            culture: null)!;
    }

    private sealed class DefaultConverter<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> :
        JsonConverter<EvolvableEnum<TEnum>>
        where TEnum : struct, Enum
    {
        private readonly EvolvableEnumWireFormat _format;
        private readonly FrozenDictionary<string, JsonEncodedText> _encodedNames;

        public DefaultConverter(EvolvableEnumWireFormat format, JavaScriptEncoder encoder)
        {
            _format = format;
            _encodedNames = _createEncodedNames<TEnum, int>(encoder);
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

            _writeName(writer, value.Name, value, _encodedNames);
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
        private readonly FrozenDictionary<string, JsonEncodedText> _encodedNames;

        public Converter(EvolvableEnumWireFormat format, JavaScriptEncoder encoder)
        {
            _format = format;
            _encodedNames = _createEncodedNames<TEnum, TBacking>(encoder);
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

            _writeName(writer, value.Name, value, _encodedNames);
        }
    }

    private static JsonException _unexpectedToken<TEnum>(
        [NotPersonalData("JSON token type is a syntax marker, not a person or secret value.")] JsonTokenType jsonTokenType)
        => new($"Cannot deserialize EvolvableEnum<{typeof(TEnum).Name}> from {jsonTokenType}.");

    private static FrozenDictionary<string, JsonEncodedText> _createEncodedNames<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
        TBacking>(JavaScriptEncoder encoder)
        where TEnum : struct, Enum
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        var names = new Dictionary<string, JsonEncodedText>(StringComparer.Ordinal);
        foreach (var enumValue in Enum.GetValues<TEnum>())
        {
            var name = EvolvableEnum<TEnum, TBacking>.FromValue(enumValue).Name;
            if (name is not null)
                names.TryAdd(name, JsonEncodedText.Encode(name, encoder));
        }

        return names.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void _writeName<TValue>(
        Utf8JsonWriter writer,
        string? name,
        TValue value,
        FrozenDictionary<string, JsonEncodedText> encodedNames)
    {
        if (name is null)
            throw new EvolvableEnumConversionException(
                $"Cannot serialize '{value}' as a JSON string because it has no symbolic name.");

        if (encodedNames.TryGetValue(name, out var encodedName))
        {
            writer.WriteStringValue(encodedName);
            return;
        }

        writer.WriteStringValue(name);
    }

    private static TBacking _readNumber<TBacking>(ref Utf8JsonReader reader)
        where TBacking : struct, IBinaryInteger<TBacking>
    {
        // TBacking is exactly the type selected by its TypeCode, so CreateTruncating is an identity
        // conversion that avoids boxing; the checked casts keep the range validation.
        return Type.GetTypeCode(typeof(TBacking)) switch
        {
            TypeCode.SByte => TBacking.CreateTruncating(checked((sbyte)reader.GetInt32())),
            TypeCode.Byte => TBacking.CreateTruncating(checked((byte)reader.GetUInt32())),
            TypeCode.Int16 => TBacking.CreateTruncating(checked((short)reader.GetInt32())),
            TypeCode.UInt16 => TBacking.CreateTruncating(checked((ushort)reader.GetUInt32())),
            TypeCode.Int32 => TBacking.CreateTruncating(reader.GetInt32()),
            TypeCode.UInt32 => TBacking.CreateTruncating(reader.GetUInt32()),
            TypeCode.Int64 => TBacking.CreateTruncating(reader.GetInt64()),
            TypeCode.UInt64 => TBacking.CreateTruncating(reader.GetUInt64()),
            _ => throw new NotSupportedException($"Unsupported evolvable enum backing type {typeof(TBacking)}."),
        };
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
