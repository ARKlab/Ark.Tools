// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Core;

using ProtoBuf;

using System.Numerics;

namespace Ark.Tools.Protobuf;

/// <summary>protobuf-net surrogate for an <see cref="int"/>-backed evolvable enum.</summary>
/// <typeparam name="TEnum">The wrapped <see cref="int"/>-backed enum.</typeparam>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "Conversions implement the protobuf-net surrogate contract.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape.")]
[ProtoContract]
public struct EvolvableEnumSurrogate<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Gets or sets the exact numeric value.</summary>
    [ProtoMember(1)]
    public int Value { get; set; }

    /// <summary>Converts to the protobuf surrogate.</summary>
    public static implicit operator EvolvableEnumSurrogate<TEnum>(EvolvableEnum<TEnum> value)
        => new() { Value = value.ToNumber() };

    /// <summary>Converts from the protobuf surrogate.</summary>
    public static implicit operator EvolvableEnum<TEnum>(EvolvableEnumSurrogate<TEnum> value)
        => EvolvableEnum<TEnum>.FromNumber(value.Value);
}

/// <summary>protobuf-net surrogate using an enum's exact integral backing type.</summary>
/// <typeparam name="TEnum">The wrapped enum.</typeparam>
/// <typeparam name="TBacking">The enum's exact integral backing type.</typeparam>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "Conversions implement the protobuf-net surrogate contract.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape.")]
[ProtoContract]
public struct EvolvableEnumSurrogate<TEnum, TBacking>
    where TEnum : struct, Enum
    where TBacking : struct, IBinaryInteger<TBacking>
{
    /// <summary>Gets or sets the exact numeric value.</summary>
    [ProtoMember(1)]
    public TBacking Value { get; set; }

    /// <summary>Converts to the protobuf surrogate.</summary>
    public static implicit operator EvolvableEnumSurrogate<TEnum, TBacking>(
        EvolvableEnum<TEnum, TBacking> value) => new() { Value = value.ToNumber() };

    /// <summary>Converts from the protobuf surrogate.</summary>
    public static implicit operator EvolvableEnum<TEnum, TBacking>(
        EvolvableEnumSurrogate<TEnum, TBacking> value)
        => EvolvableEnum<TEnum, TBacking>.FromNumber(value.Value);
}
