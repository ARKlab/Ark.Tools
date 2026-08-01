// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
namespace Ark.Tools.Core;

/// <summary>
/// A transport-neutral wrapper around a strict <typeparamref name="TEnum"/> that lets a contract add
/// new members later without breaking clients that were built against an older member set.
/// </summary>
/// <remarks>
/// <para>
/// A plain C# enum property is a <em>strict</em> contract member: every transport validates the
/// value against the declared members, and an unrecognized value is a deserialization error. Wrap
/// the property type in <see cref="EvolvableEnum{TEnum}"/> to opt into an <em>evolvable</em>
/// representation instead: unknown values never throw. A newly added server-side member simply
/// arrives as <see cref="IsDefined"/> <see langword="false"/> to an older client, which can still
/// read the raw <see cref="Name"/> or numeric value and decide how to handle it.
/// </para>
/// <para>
/// <typeparamref name="TEnum"/> must declare an explicit zero-valued member named <c>NOT_SET</c> so
/// that <see langword="default"/>(<see cref="EvolvableEnum{TEnum}"/>) — the value produced when a
/// non-nullable property is omitted from a payload — always resolves to a defined, intentional
/// value instead of an arbitrary member. <typeparamref name="TEnum"/> must not be a
/// <see cref="FlagsAttribute"/> enum: combined bit flags cannot round-trip through a single
/// evolvable value. Both rules are enforced the first time <see cref="EvolvableEnum{TEnum}"/> is
/// used for a given <typeparamref name="TEnum"/>.
/// </para>
/// <para>
/// The wrapper preserves the wrapped enum's own underlying integral type (signed or unsigned, any
/// width from <see cref="sbyte"/>/<see cref="byte"/> to <see cref="long"/>/<see cref="ulong"/>), so a
/// numeric representation never loses sign or magnitude. Binary transports (protobuf, MessagePack)
/// always carry the numeric value; JSON and SQL default to the symbolic name and can opt into the
/// numeric value explicitly. Converting to a representation that is not available (for example,
/// asking for the numeric value of a value that was produced from an unrecognized name) throws
/// <see cref="EvolvableEnumConversionException"/> rather than silently corrupting data.
/// </para>
/// </remarks>
/// <typeparam name="TEnum">
/// The wrapped enum type. Must declare an explicit zero-valued member named <c>NOT_SET</c> and must
/// not be decorated with <see cref="FlagsAttribute"/>.
/// </typeparam>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "FromValue/explicit cast to TEnum are the named alternates for the implicit/explicit conversion operators.")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The 'Enum' suffix is the intentional, spec-mandated name for this enum-wrapping value type (analogous to Nullable<T>).")]
public readonly partial struct EvolvableEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum> : IEquatable<EvolvableEnum<TEnum>>
    where TEnum : struct, Enum
{
    // Bit-pattern of the numeric value, stored using the wrapped enum's own width via a
    // sign/zero-extending widen to Int64 (see ToBits/FromBits). Meaningful only when !_isNameOnly.
    private readonly long _bits;

    // Set only when this value was produced from a symbolic name that does not map to any
    // declared member of TEnum (an "unknown-string" value): there is no numeric representation.
    private readonly string? _unknownName;

    // Discriminates the two "unknown" states. Defaults to false so that default(EvolvableEnum<TEnum>)
    // is the numeric zero value (NOT_SET), never the name-only state.
    private readonly bool _isNameOnly;

    private static readonly TypeCode _underlyingTypeCode;
    private static readonly Dictionary<long, string> _bitsToName;
    private static readonly Dictionary<string, long> _nameToBits;

    [SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "The static constructor validates TEnum (rejecting [Flags] enums and enums without NOT_SET) before building the lookup tables; the two concerns must run in this order.")]
    [SuppressMessage("Usage", "CA2207:Initialize value type static fields inline", Justification = "Static fields depend on TEnum validation that must run first; see justification above.")]
    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Failing fast the first time an invalid TEnum is used (missing NOT_SET or [Flags]) is the intended contract-validation behavior of this type.")]
    static EvolvableEnum()
    {
        var enumType = typeof(TEnum);

        if (enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
            throw new NotSupportedException(
                $"EvolvableEnum<{enumType.Name}> does not support [Flags] enums: combined bit flags cannot round-trip through a single evolvable value.");

        _underlyingTypeCode = Type.GetTypeCode(Enum.GetUnderlyingType(enumType));

        var names = Enum.GetNames(enumType);
        var values = (TEnum[])Enum.GetValues(enumType);
        _bitsToName = new Dictionary<long, string>(names.Length);
        _nameToBits = new Dictionary<string, long>(names.Length, StringComparer.Ordinal);
        for (var i = 0; i < names.Length; i++)
        {
            var bits = ToBits(values[i]);
            _bitsToName.TryAdd(bits, names[i]); // first declared alias wins for display
            _nameToBits[names[i]] = bits;
        }

        if (!_nameToBits.TryGetValue("NOT_SET", out var notSetBits) || notSetBits != 0)
            throw new InvalidOperationException(
                $"EvolvableEnum<{enumType.Name}> requires an explicit zero-valued member named 'NOT_SET' so that omitted non-nullable values default safely.");
    }

    private EvolvableEnum(long bits)
    {
        _bits = bits;
        _unknownName = null;
        _isNameOnly = false;
    }

    private EvolvableEnum(string unknownName)
    {
        _bits = 0;
        _unknownName = unknownName;
        _isNameOnly = true;
    }

    /// <summary>Gets the zero-valued, always-defined <c>NOT_SET</c> value, the safe default for an omitted non-nullable value.</summary>
    public static EvolvableEnum<TEnum> NotSet => default;

    /// <summary>Gets a value indicating whether the underlying integral type of <typeparamref name="TEnum"/> is unsigned.</summary>
    public static bool IsUnsignedUnderlyingType => _underlyingTypeCode is TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64;

    /// <summary>Gets a value indicating whether this value corresponds to a member declared on <typeparamref name="TEnum"/>.</summary>
    public bool IsDefined => !_isNameOnly && _bitsToName.ContainsKey(_bits);

    /// <summary>
    /// Gets a value indicating whether a numeric representation is available for this value. This is
    /// <see langword="false"/> only for a value produced from a symbolic name that does not map to any
    /// declared member (for example, an unrecognized name read from a JSON payload).
    /// </summary>
    public bool HasNumericValue => !_isNameOnly;

    /// <summary>Gets the strict enum value when <see cref="IsDefined"/> is <see langword="true"/>; otherwise <see langword="null"/>.</summary>
    public TEnum? Value => IsDefined ? FromBits(_bits) : null;

    /// <summary>
    /// Gets the symbolic name: the declared member name when <see cref="IsDefined"/> is
    /// <see langword="true"/>, or the preserved unrecognized name when this value was produced from an
    /// unknown string. Returns <see langword="null"/> only for an unknown numeric value that has no
    /// associated name.
    /// </summary>
    public string? Name => _isNameOnly ? _unknownName : (_bitsToName.TryGetValue(_bits, out var name) ? name : null);

    /// <summary>Wraps a strict enum value as its evolvable equivalent.</summary>
    public static implicit operator EvolvableEnum<TEnum>(TEnum value) => FromValue(value);

    /// <summary>
    /// Converts back to the strict enum type. Throws <see cref="EvolvableEnumConversionException"/>
    /// when the value is not <see cref="IsDefined"/>.
    /// </summary>
    public static explicit operator TEnum(EvolvableEnum<TEnum> value) => value.Value
        ?? throw new EvolvableEnumConversionException($"Cannot convert '{value}' to {typeof(TEnum).Name}: the value does not match a declared member.");

    /// <summary>Wraps a strict enum value as its evolvable equivalent.</summary>
    /// <param name="value">The strict enum value.</param>
    public static EvolvableEnum<TEnum> FromValue(TEnum value) => new(ToBits(value));

    /// <summary>
    /// Wraps a raw signed numeric value. The result is <see cref="IsDefined"/> when the number matches
    /// a declared member; otherwise it is retained as an unknown numeric value.
    /// </summary>
    /// <param name="number">The numeric wire value.</param>
    public static EvolvableEnum<TEnum> FromNumber(long number) => new(number);

    /// <summary>
    /// Wraps a raw unsigned numeric value. The result is <see cref="IsDefined"/> when the number
    /// matches a declared member; otherwise it is retained as an unknown numeric value.
    /// </summary>
    /// <param name="number">The numeric wire value.</param>
    public static EvolvableEnum<TEnum> FromNumber(ulong number) => new(unchecked((long)number));

    /// <summary>
    /// Wraps a symbolic name. The result is <see cref="IsDefined"/> when the name matches a declared
    /// member; otherwise the name is retained verbatim as an unknown-string value with no numeric
    /// representation.
    /// </summary>
    /// <param name="name">The symbolic wire name.</param>
    public static EvolvableEnum<TEnum> FromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _nameToBits.TryGetValue(name, out var bits) ? new EvolvableEnum<TEnum>(bits) : new EvolvableEnum<TEnum>(name);
    }

    /// <summary>Gets the numeric value as a signed 64-bit integer.</summary>
    /// <exception cref="EvolvableEnumConversionException">The value has no numeric representation (<see cref="HasNumericValue"/> is <see langword="false"/>).</exception>
    /// <remarks>For an unsigned underlying type whose value exceeds <see cref="long.MaxValue"/>, prefer <see cref="ToUInt64"/>.</remarks>
    public long ToInt64() => HasNumericValue
        ? _bits
        : throw new EvolvableEnumConversionException($"Cannot convert '{this}' to a number: the value has no numeric representation.");

    /// <summary>Gets the numeric value as an unsigned 64-bit integer, preserving magnitude for unsigned underlying types.</summary>
    /// <exception cref="EvolvableEnumConversionException">The value has no numeric representation (<see cref="HasNumericValue"/> is <see langword="false"/>).</exception>
    public ulong ToUInt64() => HasNumericValue
        ? unchecked((ulong)_bits)
        : throw new EvolvableEnumConversionException($"Cannot convert '{this}' to a number: the value has no numeric representation.");

    /// <summary>
    /// Gets the numeric value boxed as the wrapped enum's own underlying integral type (for example
    /// <see cref="byte"/> or <see cref="uint"/>), preserving its exact sign and width.
    /// </summary>
    /// <exception cref="EvolvableEnumConversionException">The value has no numeric representation (<see cref="HasNumericValue"/> is <see langword="false"/>).</exception>
    public object ToUnderlyingNumber() => HasNumericValue
        ? BitsToUnderlyingBoxed(_bits)
        : throw new EvolvableEnumConversionException($"Cannot convert '{this}' to a number: the value has no numeric representation.");

    /// <inheritdoc />
    public bool Equals(EvolvableEnum<TEnum> other) => _isNameOnly == other._isNameOnly
        && (_isNameOnly ? string.Equals(_unknownName, other._unknownName, StringComparison.Ordinal) : _bits == other._bits);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EvolvableEnum<TEnum> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _isNameOnly ? HashCode.Combine(true, _unknownName) : HashCode.Combine(false, _bits);

    /// <summary>Determines whether two values are equal.</summary>
    public static bool operator ==(EvolvableEnum<TEnum> left, EvolvableEnum<TEnum> right) => left.Equals(right);

    /// <summary>Determines whether two values are different.</summary>
    public static bool operator !=(EvolvableEnum<TEnum> left, EvolvableEnum<TEnum> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Name ?? (IsUnsignedUnderlyingType
        ? ToUInt64().ToString(CultureInfo.InvariantCulture)
        : ToInt64().ToString(CultureInfo.InvariantCulture));

    // Widens the enum's own underlying-typed value into a 64-bit container. Byte/UInt16/UInt32 widen
    // by magnitude (they always fit); only UInt64 needs an unchecked bit-pattern reinterpretation
    // because ulong.MaxValue exceeds long.MaxValue. FromBits/BitsToUnderlyingBoxed reverse this
    // exactly by truncating back to the original width, so the round trip is always bit-exact.
    private static long ToBits(TEnum value) => _underlyingTypeCode switch
    {
        TypeCode.SByte => (sbyte)(object)value,
        TypeCode.Byte => (byte)(object)value,
        TypeCode.Int16 => (short)(object)value,
        TypeCode.UInt16 => (ushort)(object)value,
        TypeCode.Int32 => (int)(object)value,
        TypeCode.UInt32 => (uint)(object)value,
        TypeCode.Int64 => (long)(object)value,
        TypeCode.UInt64 => unchecked((long)(ulong)(object)value),
        _ => throw new NotSupportedException($"EvolvableEnum<{typeof(TEnum).Name}> does not support underlying type {_underlyingTypeCode}."),
    };

    private static TEnum FromBits(long bits) => (TEnum)Enum.ToObject(typeof(TEnum), BitsToUnderlyingBoxed(bits));

    private static object BitsToUnderlyingBoxed(long bits) => _underlyingTypeCode switch
    {
        TypeCode.SByte => (sbyte)bits,
        TypeCode.Byte => (byte)bits,
        TypeCode.Int16 => (short)bits,
        TypeCode.UInt16 => (ushort)bits,
        TypeCode.Int32 => (int)bits,
        TypeCode.UInt32 => (uint)bits,
        TypeCode.Int64 => bits,
        TypeCode.UInt64 => unchecked((ulong)bits),
        _ => throw new NotSupportedException($"EvolvableEnum<{typeof(TEnum).Name}> does not support underlying type {_underlyingTypeCode}."),
    };
}
