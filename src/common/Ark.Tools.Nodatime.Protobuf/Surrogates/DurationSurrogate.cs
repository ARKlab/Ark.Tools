// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

using NodaTime.Serialization.Protobuf;

using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="Duration"/>, using the
/// <c>google.protobuf.Duration</c> wire shape.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape, not a value object.")]
[StructLayout(LayoutKind.Auto)]
[ProtoContract(Name = "Duration")]
public struct DurationSurrogate
{
    /// <summary>Gets or sets the seconds.</summary>
    [ProtoMember(1)]
    public long Seconds { get; set; }

    /// <summary>Gets or sets the nanosecond fraction.</summary>
    [ProtoMember(2)]
    public int Nanos { get; set; }

    /// <summary>Converts a duration into its protobuf representation.</summary>
    public static implicit operator DurationSurrogate(NodaTime.Duration value)
    {
        var duration = value.ToProtobufDuration();
        return new DurationSurrogate { Seconds = duration.Seconds, Nanos = duration.Nanos };
    }

    /// <summary>Converts a protobuf representation into a duration.</summary>
    public static implicit operator NodaTime.Duration(DurationSurrogate value)
    {
        return new Google.Protobuf.WellKnownTypes.Duration
        {
            Seconds = value.Seconds,
            Nanos = value.Nanos,
        }.ToNodaDuration();
    }
}
