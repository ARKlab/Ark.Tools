// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Google.Protobuf.WellKnownTypes;

using ProtoBuf;

using NodaTime.Serialization.Protobuf;

using System.Runtime.InteropServices;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="Instant"/>, using the
/// <c>google.protobuf.Timestamp</c> wire shape.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[SuppressMessage("Design", "CA1815:Override equals and operator equals on value types", Justification = "The surrogate is a serialization shape, not a value object.")]
[StructLayout(LayoutKind.Auto)]
[ProtoContract(Name = "Timestamp")]
public struct InstantSurrogate
{
    /// <summary>Gets or sets the seconds since the Unix epoch.</summary>
    [ProtoMember(1)]
    public long Seconds { get; set; }

    /// <summary>Gets or sets the nanosecond fraction.</summary>
    [ProtoMember(2)]
    public int Nanos { get; set; }

    /// <summary>Converts an instant into its protobuf representation.</summary>
    public static implicit operator InstantSurrogate(Instant value)
    {
        var timestamp = value.ToTimestamp();
        return new InstantSurrogate { Seconds = timestamp.Seconds, Nanos = timestamp.Nanos };
    }

    /// <summary>Converts a protobuf representation into an instant.</summary>
    public static implicit operator Instant(InstantSurrogate value)
    {
        return new Timestamp { Seconds = value.Seconds, Nanos = value.Nanos }.ToInstant();
    }
}
