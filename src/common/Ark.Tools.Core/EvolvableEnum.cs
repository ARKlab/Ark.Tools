// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Collections.Frozen;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;

namespace Ark.Tools.Core;

/// <summary>
/// Wraps an <see cref="int"/>-backed enum so unknown names and values can round-trip safely.
/// </summary>
/// <typeparam name="TEnum">The wrapped <see cref="int"/>-backed enum type.</typeparam>
/// <remarks>
/// Use <see cref="EvolvableEnum{TEnum,TBacking}"/> when the enum has a backing type other than
/// <see cref="int"/>. The enum must declare <c>NOT_SET = 0</c> and must not use
/// <see cref="FlagsAttribute"/>.
/// </remarks>
[TypeConverter(typeof(EvolvableEnumTypeConverter))]
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "FromValue and Value are the named alternatives.")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Enum is the intentional name for this enum wrapper.")]
public readonly struct EvolvableEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> :
    IEquatable<EvolvableEnum<TEnum>>,
    IParsable<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    private readonly EvolvableEnum<TEnum, int> _value;

    private EvolvableEnum(EvolvableEnum<TEnum, int> value)
    {
        _value = value;
    }

    /// <summary>Gets the defined <c>NOT_SET</c> value.</summary>
    public static EvolvableEnum<TEnum> NotSet => default;

    /// <summary>Gets whether this value maps to a declared enum member.</summary>
    public bool IsDefined => _value.IsDefined;

    /// <summary>Gets whether this value has a numeric representation.</summary>
    public bool HasNumericValue => _value.HasNumericValue;

    /// <summary>Gets the declared enum value, or <see langword="null"/> for an unknown value.</summary>
    public TEnum? Value => _value.Value;

    /// <summary>Gets the known or preserved unknown symbolic name.</summary>
    public string? Name => _value.Name;

    /// <summary>Wraps a strict enum value.</summary>
    public static implicit operator EvolvableEnum<TEnum>(TEnum value) => FromValue(value);

    /// <summary>Converts a known value to the strict enum type.</summary>
    public static explicit operator TEnum(EvolvableEnum<TEnum> value) => (TEnum)value._value;

    /// <summary>Wraps a strict enum value.</summary>
    public static EvolvableEnum<TEnum> FromValue(TEnum value) => new(EvolvableEnum<TEnum, int>.FromValue(value));

    /// <summary>Wraps a numeric value using the enum's exact <see cref="int"/> backing type.</summary>
    public static EvolvableEnum<TEnum> FromNumber(int number) => new(EvolvableEnum<TEnum, int>.FromNumber(number));

    /// <summary>Wraps a known or unknown symbolic name.</summary>
    public static EvolvableEnum<TEnum> FromName(string name) => new(EvolvableEnum<TEnum, int>.FromName(name));

    /// <summary>Gets the numeric value using the enum's exact <see cref="int"/> backing type.</summary>
    public int ToNumber() => _value.ToNumber();

    /// <summary>Parses a known name, unknown name, or invariant numeric value.</summary>
    public static EvolvableEnum<TEnum> Parse(string s, IFormatProvider? provider)
        => new(EvolvableEnum<TEnum, int>.Parse(s, provider));

    /// <summary>Tries to parse a known name, unknown name, or invariant numeric value.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out EvolvableEnum<TEnum> result)
    {
        if (EvolvableEnum<TEnum, int>.TryParse(s, provider, out var parsed))
        {
            result = new EvolvableEnum<TEnum>(parsed);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Parses a known name, unknown name, or invariant numeric value.</summary>
    public static EvolvableEnum<TEnum> Parse(string s) => Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Tries to parse a known name, unknown name, or invariant numeric value.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, out EvolvableEnum<TEnum> result)
        => TryParse(s, CultureInfo.InvariantCulture, out result);

    /// <inheritdoc />
    public bool Equals(EvolvableEnum<TEnum> other) => _value.Equals(other._value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EvolvableEnum<TEnum> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>Determines whether two values are equal.</summary>
    public static bool operator ==(EvolvableEnum<TEnum> left, EvolvableEnum<TEnum> right) => left.Equals(right);

    /// <summary>Determines whether two values are different.</summary>
    public static bool operator !=(EvolvableEnum<TEnum> left, EvolvableEnum<TEnum> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _value.ToString();
}

/// <summary>
/// Wraps an enum using its exact integral backing type so unknown names and values can round-trip
/// safely.
/// </summary>
/// <typeparam name="TEnum">The wrapped enum type.</typeparam>
/// <typeparam name="TBacking">The enum's exact integral backing type.</typeparam>
/// <remarks>
/// <typeparamref name="TBacking"/> must exactly match the backing type declared by
/// <typeparamref name="TEnum"/>. The enum must declare <c>NOT_SET = 0</c> and must not use
/// <see cref="FlagsAttribute"/>.
/// </remarks>
[TypeConverter(typeof(EvolvableEnumTypeConverter))]
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "FromValue and Value are the named alternatives.")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Enum is the intentional name for this enum wrapper.")]
public readonly struct EvolvableEnum<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum,
    TBacking> :
    IEquatable<EvolvableEnum<TEnum, TBacking>>,
    IParsable<EvolvableEnum<TEnum, TBacking>>
    where TEnum : struct, Enum
    where TBacking : struct, IBinaryInteger<TBacking>
{
    private readonly TBacking _number;
    private readonly string? _unknownName;

    private static readonly FrozenDictionary<TBacking, string> _numberToName;
    private static readonly FrozenDictionary<string, TBacking> _nameToNumber;
    private static readonly string?[]? _numberToNameArray;
    private static readonly TBacking _numberToNameArrayMinimum;
    private static readonly TBacking _numberToNameArrayMaximum;

    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Runtime validation remains required when analyzers are disabled.")]
    [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "Lookup creation follows generic argument validation.")]
    [SuppressMessage("Usage", "CA2207:Initialize value type static fields inline", Justification = "Lookup creation follows generic argument validation.")]
    static EvolvableEnum()
    {
        var enumType = typeof(TEnum);
        var backingType = Enum.GetUnderlyingType(enumType);

        if (backingType != typeof(TBacking))
            throw new InvalidOperationException(
                $"EvolvableEnum<{enumType.Name}, {typeof(TBacking).Name}> requires {backingType.Name}, the enum's exact backing type.");

        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
            throw new NotSupportedException(
                $"EvolvableEnum<{enumType.Name}, {typeof(TBacking).Name}> does not support [Flags] enums.");

        var names = Enum.GetNames(enumType);
        var values = (TEnum[])Enum.GetValues(enumType);
        var numberToName = new Dictionary<TBacking, string>(names.Length);
        var numericValues = new TBacking[names.Length];
        var displayNames = new string[names.Length];
        var allNames = new Dictionary<string, TBacking>(StringComparer.Ordinal);

        for (var i = 0; i < names.Length; i++)
        {
            var number = (TBacking)Convert.ChangeType(values[i], typeof(TBacking), CultureInfo.InvariantCulture);
            numericValues[i] = number;
            var field = enumType.GetField(names[i])!;
            var annotatedNames = _getAnnotatedNames(field);
            var displayName = annotatedNames.DisplayName;
            numberToName.TryAdd(number, displayName);
            displayNames[i] = displayName;
            _addName(allNames, names[i], number, enumType);
            foreach (var attributeName in annotatedNames.Names)
            {
                if (!string.Equals(names[i], attributeName, StringComparison.Ordinal))
                    _addName(allNames, attributeName, number, enumType);
            }
        }

        _numberToName = numberToName.ToFrozenDictionary();
        _nameToNumber = allNames.ToFrozenDictionary(StringComparer.Ordinal);

        if (!_nameToNumber.TryGetValue("NOT_SET", out var notSet) || notSet != TBacking.Zero)
            throw new InvalidOperationException(
                $"EvolvableEnum<{enumType.Name}, {typeof(TBacking).Name}> requires an explicit zero-valued member named 'NOT_SET'.");

        var minimum = numericValues.Select(static value => BigInteger.CreateChecked(value)).Min();
        var maximum = numericValues.Select(static value => BigInteger.CreateChecked(value)).Max();
        var range = maximum - minimum + BigInteger.One;
        if (range <= 4096 && range * 90 <= numericValues.Length * 100)
        {
            _numberToNameArrayMinimum = TBacking.CreateChecked(minimum);
            _numberToNameArrayMaximum = TBacking.CreateChecked(maximum);
            _numberToNameArray = new string?[checked((int)range)];
            for (var i = 0; i < numericValues.Length; i++)
            {
                var index = checked((int)(BigInteger.CreateChecked(numericValues[i]) - minimum));
                _numberToNameArray[index] ??= displayNames[i];
            }
        }
    }

    private EvolvableEnum(TBacking number)
    {
        _number = number;
        _unknownName = null;
    }

    private EvolvableEnum(string unknownName)
    {
        _number = TBacking.Zero;
        _unknownName = unknownName;
    }

    /// <summary>Gets the defined <c>NOT_SET</c> value.</summary>
    public static EvolvableEnum<TEnum, TBacking> NotSet => default;

    /// <summary>Gets whether this value maps to a declared enum member.</summary>
    public bool IsDefined => _unknownName is null && _getName(_number) is not null;

    /// <summary>Gets whether this value has a numeric representation.</summary>
    public bool HasNumericValue => _unknownName is null;

    /// <summary>Gets the declared enum value, or <see langword="null"/> for an unknown value.</summary>
    public TEnum? Value => IsDefined ? (TEnum)Enum.ToObject(typeof(TEnum), _number) : null;

    /// <summary>Gets the known or preserved unknown symbolic name.</summary>
    public string? Name => _unknownName ?? _getName(_number);

    /// <summary>Wraps a strict enum value.</summary>
    public static implicit operator EvolvableEnum<TEnum, TBacking>(TEnum value) => FromValue(value);

    /// <summary>Converts a known value to the strict enum type.</summary>
    public static explicit operator TEnum(EvolvableEnum<TEnum, TBacking> value) => value.Value
        ?? throw new EvolvableEnumConversionException(
            $"Cannot convert '{value}' to {typeof(TEnum).Name}: the value does not match a declared member.");

    /// <summary>Wraps a strict enum value.</summary>
    public static EvolvableEnum<TEnum, TBacking> FromValue(TEnum value)
        => new((TBacking)Convert.ChangeType(value, typeof(TBacking), CultureInfo.InvariantCulture));

    /// <summary>Wraps a numeric value using the enum's exact backing type.</summary>
    public static EvolvableEnum<TEnum, TBacking> FromNumber(TBacking number) => new(number);

    /// <summary>Wraps a known or unknown symbolic name.</summary>
    public static EvolvableEnum<TEnum, TBacking> FromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _nameToNumber.TryGetValue(name, out var number)
            ? new EvolvableEnum<TEnum, TBacking>(number)
            : new EvolvableEnum<TEnum, TBacking>(name);
    }

    /// <summary>Gets the numeric value using the enum's exact backing type.</summary>
    public TBacking ToNumber() => _unknownName is null
        ? _number
        : throw new EvolvableEnumConversionException(
            $"Cannot convert '{this}' to {typeof(TBacking).Name}: the value has no numeric representation.");

    /// <summary>Parses a known name, unknown name, or invariant numeric value.</summary>
    public static EvolvableEnum<TEnum, TBacking> Parse(string s, IFormatProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"'{s}' is not a valid {typeof(EvolvableEnum<TEnum, TBacking>)} value.");

        return result;
    }

    /// <summary>Tries to parse a known name, unknown name, or invariant numeric value.</summary>
    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        out EvolvableEnum<TEnum, TBacking> result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;
        }

        if (_nameToNumber.TryGetValue(s, out var knownNumber))
        {
            result = new EvolvableEnum<TEnum, TBacking>(knownNumber);
            return true;
        }

        if (TBacking.TryParse(s, NumberStyles.Integer, provider ?? CultureInfo.InvariantCulture, out var number))
        {
            result = new EvolvableEnum<TEnum, TBacking>(number);
            return true;
        }

        if (char.IsDigit(s[0]) || s[0] is '+' or '-')
        {
            result = default;
            return false;
        }

        result = new EvolvableEnum<TEnum, TBacking>(s);
        return true;
    }

    /// <summary>Parses a known name, unknown name, or invariant numeric value.</summary>
    public static EvolvableEnum<TEnum, TBacking> Parse(string s)
        => Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Tries to parse a known name, unknown name, or invariant numeric value.</summary>
    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        out EvolvableEnum<TEnum, TBacking> result)
        => TryParse(s, CultureInfo.InvariantCulture, out result);

    /// <inheritdoc />
    public bool Equals(EvolvableEnum<TEnum, TBacking> other)
        => _unknownName is null
            ? other._unknownName is null && _number == other._number
            : string.Equals(_unknownName, other._unknownName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is EvolvableEnum<TEnum, TBacking> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => _unknownName is null ? HashCode.Combine(false, _number) : HashCode.Combine(true, _unknownName);

    /// <summary>Determines whether two values are equal.</summary>
    public static bool operator ==(
        EvolvableEnum<TEnum, TBacking> left,
        EvolvableEnum<TEnum, TBacking> right) => left.Equals(right);

    /// <summary>Determines whether two values are different.</summary>
    public static bool operator !=(
        EvolvableEnum<TEnum, TBacking> left,
        EvolvableEnum<TEnum, TBacking> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Name ?? ToNumber().ToString(null, CultureInfo.InvariantCulture);

    private static string? _getName(TBacking number)
    {
        if (_numberToNameArray is not null)
        {
            if (number < _numberToNameArrayMinimum || number > _numberToNameArrayMaximum)
                return null;

            // Use unsigned wrapping to avoid signed overflow before bounded int conversion.
            var index = int.CreateTruncating(unchecked(
                uint.CreateTruncating(number) - uint.CreateTruncating(_numberToNameArrayMinimum)));
            return _numberToNameArray[index];
        }

        return _numberToName.TryGetValue(number, out var name) ? name : null;
    }

    private static (string DisplayName, string[] Names) _getAnnotatedNames(FieldInfo field)
    {
        var attributes = field.GetCustomAttributes(inherit: false);
        var enumMember = attributes.OfType<EnumMemberAttribute>().FirstOrDefault()?.Value;
        var display = attributes.OfType<DisplayAttribute>().FirstOrDefault()?.GetName();
        var displayName = attributes.OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName;
        var names = new[] { enumMember, display, displayName }
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return (enumMember ?? display ?? displayName ?? field.Name, names);
    }

    private static void _addName(
        Dictionary<string, TBacking> names,
        string name,
        TBacking number,
        Type enumType)
    {
        if (names.TryGetValue(name, out var existing) && existing != number)
            throw new InvalidOperationException($"Enum '{enumType.Name}' contains duplicate evolvable name '{name}'.");

        names.TryAdd(name, number);
    }
}
