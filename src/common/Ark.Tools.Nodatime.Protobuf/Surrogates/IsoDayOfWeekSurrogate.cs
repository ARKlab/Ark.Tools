// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

using NodaTime.Serialization.Protobuf;

using ProtoDayOfWeek = Google.Type.DayOfWeek;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="IsoDayOfWeek"/>, using the
/// <c>google.type.DayOfWeek</c> enum values.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape, not a value object.")]
[ProtoContract(Name = "DayOfWeek")]
public struct IsoDayOfWeekSurrogate
{
    /// <summary>Gets or sets the common-proto enum value.</summary>
    [ProtoMember(1)]
    public ProtoDayOfWeek Value { get; set; }

    /// <summary>Converts an ISO day into its protobuf representation.</summary>
    public static implicit operator IsoDayOfWeekSurrogate(IsoDayOfWeek value)
    {
        return new IsoDayOfWeekSurrogate { Value = value.ToProtobufDayOfWeek() };
    }

    /// <summary>Converts a protobuf representation into an ISO day.</summary>
    public static implicit operator IsoDayOfWeek(IsoDayOfWeekSurrogate value)
    {
        return value.Value.ToIsoDayOfWeek();
    }
}
