// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="ZonedDateTime"/>, using the
/// <c>google.type.DateTime</c> wire shape with <c>time_zone</c> populated.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[ProtoContract]
public sealed class ZonedDateTimeSurrogate
{
    /// <summary>Gets or sets the ISO year.</summary>
    [ProtoMember(1)]
    public int Year { get; set; }

    /// <summary>Gets or sets the month of year.</summary>
    [ProtoMember(2)]
    public int Month { get; set; }

    /// <summary>Gets or sets the day of month.</summary>
    [ProtoMember(3)]
    public int Day { get; set; }

    /// <summary>Gets or sets the hour.</summary>
    [ProtoMember(4)]
    public int Hours { get; set; }

    /// <summary>Gets or sets the minute.</summary>
    [ProtoMember(5)]
    public int Minutes { get; set; }

    /// <summary>Gets or sets the second.</summary>
    [ProtoMember(6)]
    public int Seconds { get; set; }

    /// <summary>Gets or sets the nanosecond.</summary>
    [ProtoMember(7)]
    public int Nanos { get; set; }

    /// <summary>Gets or sets the time-zone identifier.</summary>
    [ProtoMember(9)]
    public GoogleTimeZoneSurrogate? TimeZone { get; set; }

    /// <summary>Converts a <see cref="ZonedDateTime"/> into its surrogate.</summary>
    public static implicit operator ZonedDateTimeSurrogate(ZonedDateTime value)
    {
        var local = value.LocalDateTime;
        return new ZonedDateTimeSurrogate
        {
            Year = local.Year,
            Month = local.Month,
            Day = local.Day,
            Hours = local.Hour,
            Minutes = local.Minute,
            Seconds = local.Second,
            Nanos = local.NanosecondOfSecond,
            TimeZone = new GoogleTimeZoneSurrogate { Id = value.Zone.Id },
        };
    }

    /// <summary>Converts a surrogate back into a <see cref="ZonedDateTime"/>.</summary>
    public static implicit operator ZonedDateTime(ZonedDateTimeSurrogate? value)
    {
        if (value is null)
            return default;

        var local = new LocalDate(value.Year, value.Month, value.Day)
            .At(LocalTime.FromHourMinuteSecondNanosecond(
                value.Hours, value.Minutes, value.Seconds, value.Nanos));
        var zone = DateTimeZoneProviders.Tzdb[value.TimeZone?.Id ?? "Etc/UTC"];
        return local.InZoneLeniently(zone);
    }
}
