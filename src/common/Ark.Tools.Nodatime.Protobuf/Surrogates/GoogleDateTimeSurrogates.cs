// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>protobuf-net representation of <c>google.protobuf.Duration</c>.</summary>
[ProtoContract(Name = "Duration")]
public sealed class GoogleDurationSurrogate
{
    /// <summary>Gets or sets the whole seconds.</summary>
    [ProtoMember(1)]
    public long Seconds { get; set; }

    /// <summary>Gets or sets the fractional nanoseconds.</summary>
    [ProtoMember(2)]
    public int Nanos { get; set; }
}

/// <summary>protobuf-net representation of <c>google.type.TimeZone</c>.</summary>
[ProtoContract(Name = "TimeZone")]
public sealed class GoogleTimeZoneSurrogate
{
    /// <summary>Gets or sets the IANA time-zone identifier.</summary>
    [ProtoMember(1)]
    public string? Id { get; set; }

    /// <summary>Gets or sets the IANA time-zone database version.</summary>
    [ProtoMember(2)]
    public string? Version { get; set; }
}
