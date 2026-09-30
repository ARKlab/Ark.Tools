// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ark.Tools.SystemTextJson;

/// <summary>
/// Converts enums to and from strings. Member names are resolved from <see cref="JsonStringEnumMemberNameAttribute"/>,
/// then <see cref="EnumMemberAttribute"/>, then the naming policy, then the member name.
/// </summary>
/// <remarks>
/// Built on <see cref="JsonStringEnumConverter{TEnum}"/>: integers are accepted, undefined values are written as numbers,
/// <see cref="FlagsAttribute"/> combinations are written as <c>"A, B"</c> and nullable enums are supported.
/// Unlike the built-in converter, custom names are also read case-insensitively.
/// Use <see cref="ArkJsonStringEnumConverter{TEnum}"/> for Native AOT and source-generated contexts.
/// </remarks>
// ponytail: no deserialization-failure fallback value, no options attribute and no [JsonPropertyName] on enum members (Macross-only features, unused by Ark).
public sealed class ArkJsonStringEnumConverter : JsonConverterFactory
{
    private readonly JsonNamingPolicy? _namingPolicy;
    private readonly bool _allowIntegerValues;

    /// <summary>Initializes a new instance of the <see cref="ArkJsonStringEnumConverter"/> class.</summary>
    /// <param name="namingPolicy">Optional naming policy applied to members without an explicit name.</param>
    /// <param name="allowIntegerValues">Whether integer values are accepted on read and undefined values written as numbers.</param>
    [RequiresDynamicCode("Creates an ArkJsonStringEnumConverter<TEnum> per enum type at runtime. Use ArkJsonStringEnumConverter<TEnum> for Native AOT.")]
    public ArkJsonStringEnumConverter(JsonNamingPolicy? namingPolicy = null, bool allowIntegerValues = true)
    {
        _namingPolicy = namingPolicy;
        _allowIntegerValues = allowIntegerValues;
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2071:DynamicallyAccessedMembers",
        Justification = "ILLink always keeps every field of a kept enum type, and name attributes are referenced by EnumNames; System.Text.Json relies on the same guarantee in its enum converter.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "The requirement is declared on the constructor.")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var factory = (JsonConverterFactory)Activator.CreateInstance(
            typeof(ArkJsonStringEnumConverter<>).MakeGenericType(typeToConvert),
            _namingPolicy,
            _allowIntegerValues)!;

        return factory.CreateConverter(typeToConvert, options);
    }
}

/// <summary>
/// Native AOT compatible variant of <see cref="ArkJsonStringEnumConverter"/> for a single enum type.
/// </summary>
/// <typeparam name="TEnum">The enum type.</typeparam>
public class ArkJsonStringEnumConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>
    : JsonConverterFactory
    where TEnum : struct, Enum
{
    private readonly JsonStringEnumConverter<TEnum> _builtIn;
    private readonly EnumNames _names;

    /// <summary>Initializes a new instance of the <see cref="ArkJsonStringEnumConverter{TEnum}"/> class.</summary>
    public ArkJsonStringEnumConverter()
        : this(null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArkJsonStringEnumConverter{TEnum}"/> class.</summary>
    /// <param name="namingPolicy">Optional naming policy applied to members without an explicit name.</param>
    /// <param name="allowIntegerValues">Whether integer values are accepted on read and undefined values written as numbers.</param>
    public ArkJsonStringEnumConverter(JsonNamingPolicy? namingPolicy, bool allowIntegerValues = true)
    {
        _names = new EnumNames(typeof(TEnum), namingPolicy);
        _builtIn = new JsonStringEnumConverter<TEnum>(_names, allowIntegerValues);
    }

    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(TEnum);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return new CaseInsensitiveEnumConverter<TEnum>(
            (JsonConverter<TEnum>)_builtIn.CreateConverter(typeToConvert, options)!,
            _names);
    }
}

/// <summary>
/// Naming policy mapping member names to their <see cref="EnumMemberAttribute"/> value, so the built-in converter honors it.
/// <see cref="JsonStringEnumMemberNameAttribute"/> bypasses naming policies in the built-in converter, so it keeps precedence.
/// Also canonicalizes the casing of incoming names, as the built-in converter reads custom names case-sensitively.
/// </summary>
// ponytail: DictionaryKeyPolicy, when set, is applied by STJ to the member name instead of the EnumMember value.
// Ark defaults are unaffected because GenericDictionaryWithConvertibleKey handles enum-keyed dictionaries first.
internal sealed class EnumNames : JsonNamingPolicy
{
    private readonly FrozenDictionary<string, string> _enumMembers;
    private readonly JsonNamingPolicy? _inner;
    private readonly FrozenSet<string> _exact;
    private readonly FrozenDictionary<string, string> _byIgnoreCase;

    public EnumNames(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type enumType,
        JsonNamingPolicy? inner)
    {
        _inner = inner;
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

        _enumMembers = fields
            .Select(static f => (f.Name, f.GetCustomAttribute<EnumMemberAttribute>(inherit: false)?.Value))
            .Where(static x => !string.IsNullOrEmpty(x.Value))
            .ToFrozenDictionary(static x => x.Name, static x => x.Value!, StringComparer.Ordinal);

        var jsonNames = fields
            .Select(f => f.GetCustomAttribute<JsonStringEnumMemberNameAttribute>(inherit: false)?.Name ?? ConvertName(f.Name))
            .ToList();

        _exact = jsonNames.ToFrozenSet(StringComparer.Ordinal);
        _byIgnoreCase = jsonNames
            .DistinctBy(static n => n, StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(static n => n, static n => n, StringComparer.OrdinalIgnoreCase);
    }

    public override string ConvertName(string name)
    {
        return _enumMembers.TryGetValue(name, out var value) ? value : _inner?.ConvertName(name) ?? name;
    }

    /// <summary>Returns <paramref name="value"/> itself when no name needs re-casing, otherwise the canonical form.</summary>
    public string Canonicalize(string value)
    {
        if (_exact.Contains(value))
            return value;

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        var changed = false;
        for (var i = 0; i < parts.Length; i++)
        {
            if (!_exact.Contains(parts[i]) && _byIgnoreCase.TryGetValue(parts[i], out var canonical))
            {
                parts[i] = canonical;
                changed = true;
            }
        }

        return changed ? string.Join(", ", parts) : value;
    }
}

/// <summary>Delegates to the built-in enum converter after re-casing incoming names.</summary>
internal sealed class CaseInsensitiveEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly JsonConverter<TEnum> _inner;
    private readonly EnumNames _names;

    public CaseInsensitiveEnumConverter(JsonConverter<TEnum> inner, EnumNames names)
    {
        _inner = inner;
        _names = names;
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && _tryCanonicalize(ref reader, out var canonical))
        {
            var copy = _createReader(canonical, asPropertyName: false);
            return _inner.Read(ref copy, typeToConvert, options);
        }

        return _inner.Read(ref reader, typeToConvert, options);
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }

    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (_tryCanonicalize(ref reader, out var canonical))
        {
            var copy = _createReader(canonical, asPropertyName: true);
            return _inner.ReadAsPropertyName(ref copy, typeToConvert, options);
        }

        return _inner.ReadAsPropertyName(ref reader, typeToConvert, options);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        _inner.WriteAsPropertyName(writer, value, options);
    }

    private bool _tryCanonicalize(ref Utf8JsonReader reader, [NotNullWhen(true)] out string? canonical)
    {
        var value = reader.GetString()!;
        canonical = _names.Canonicalize(value);
        if (ReferenceEquals(canonical, value))
        {
            canonical = null;
            return false;
        }

        return true;
    }

    private static Utf8JsonReader _createReader(string value, bool asPropertyName)
    {
        var quoted = "\"" + JsonEncodedText.Encode(value) + "\"";
        var json = Encoding.UTF8.GetBytes(asPropertyName ? "{" + quoted + ":null}" : quoted);

        var reader = new Utf8JsonReader(json);
        reader.Read();
        if (asPropertyName)
            reader.Read();

        return reader;
    }
}
