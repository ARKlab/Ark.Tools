// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using ProtoBuf;

namespace Ark.Tools.Core.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="EvolvableEnum{TEnum}"/>. Always carries the numeric
/// underlying value as a 64-bit integer (protobuf has no symbolic-name concept), which is why the
/// numeric representation is mandatory for this transport. Converting a value produced from an
/// unrecognized name with no numeric value (<see cref="EvolvableEnum{TEnum}.HasNumericValue"/>
/// <see langword="false"/>) fails explicitly rather than silently corrupting the value.
/// </summary>
/// <typeparam name="TEnum">The wrapped enum type.</typeparam>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape, not a value object.")]
[ProtoContract]
public struct EvolvableEnumSurrogate<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Gets or sets the raw 64-bit numeric wire value.</summary>
    [ProtoMember(1)]
    public long Value { get; set; }

    /// <summary>Converts an evolvable enum value into its protobuf surrogate representation.</summary>
    /// <exception cref="EvolvableEnumConversionException">The value has no numeric representation.</exception>
    [SuppressMessage("Design", "CA1065:Do not raise exceptions in unexpected locations", Justification = "Failing fast when the value has no numeric representation is the intended cross-form-conversion contract of EvolvableEnum<TEnum>.")]
    public static implicit operator EvolvableEnumSurrogate<TEnum>(EvolvableEnum<TEnum> value)
    {
        if (!value.HasNumericValue)
            throw new EvolvableEnumConversionException(
                $"Cannot serialize '{value}' to protobuf: the value has no numeric representation (it was produced from an unrecognized name).");

        return new EvolvableEnumSurrogate<TEnum> { Value = value.ToInt64() };
    }

    /// <summary>Converts a protobuf surrogate representation back into an evolvable enum value.</summary>
    public static implicit operator EvolvableEnum<TEnum>(EvolvableEnumSurrogate<TEnum> value)
        => EvolvableEnum<TEnum>.FromNumber(value.Value);
}
