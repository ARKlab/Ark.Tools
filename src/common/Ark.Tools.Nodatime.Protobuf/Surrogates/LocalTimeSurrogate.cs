// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Google.Type;

using ProtoBuf;

using NodaTime.Serialization.Protobuf;

using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="LocalTime"/>, using the
/// <c>google.type.TimeOfDay</c> wire shape.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape, not a value object.")]
[StructLayout(LayoutKind.Auto)]
[ProtoContract(Name = "TimeOfDay")]
public struct LocalTimeSurrogate
{
    /// <summary>Gets or sets the hour.</summary>
    [ProtoMember(1)]
    public int Hours { get; set; }

    /// <summary>Gets or sets the minute.</summary>
    [ProtoMember(2)]
    public int Minutes { get; set; }

    /// <summary>Gets or sets the second.</summary>
    [ProtoMember(3)]
    public int Seconds { get; set; }

    /// <summary>Gets or sets the nanosecond.</summary>
    [ProtoMember(4)]
    public int Nanos { get; set; }

    /// <summary>Converts a local time into its protobuf representation.</summary>
    public static implicit operator LocalTimeSurrogate(LocalTime value)
    {
        var time = value.ToTimeOfDay();
        return new LocalTimeSurrogate
        {
            Hours = time.Hours,
            Minutes = time.Minutes,
            Seconds = time.Seconds,
            Nanos = time.Nanos,
        };
    }

    /// <summary>Converts a protobuf representation into a local time.</summary>
    public static implicit operator LocalTime(LocalTimeSurrogate value)
    {
        return new TimeOfDay
        {
            Hours = value.Hours,
            Minutes = value.Minutes,
            Seconds = value.Seconds,
            Nanos = value.Nanos,
        }.ToLocalTime();
    }
}
