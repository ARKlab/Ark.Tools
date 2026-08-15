// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="LocalDateTime"/>, using the
/// <c>google.type.DateTime</c> wire shape without a time offset.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[ProtoContract]
public sealed class LocalDateTimeSurrogate
{
    /// <summary>Gets or sets the ISO year.</summary>
    [ProtoMember(1)]
    public int Year { get; set; }

    /// <summary>Gets or sets the month of year (1-12).</summary>
    [ProtoMember(2)]
    public int Month { get; set; }

    /// <summary>Gets or sets the day of month (1-based).</summary>
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

    /// <summary>Converts a <see cref="LocalDateTime"/> into its surrogate.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator LocalDateTimeSurrogate(LocalDateTime value)
        => new()
        {
            Year = value.Year,
            Month = value.Month,
            Day = value.Day,
            Hours = value.Hour,
            Minutes = value.Minute,
            Seconds = value.Second,
            Nanos = value.NanosecondOfSecond,
        };

    /// <summary>Converts a surrogate back into a <see cref="LocalDateTime"/>.</summary>
    /// <param name="value">The surrogate to convert.</param>
    public static implicit operator LocalDateTime(LocalDateTimeSurrogate? value)
        => value is null
            ? default
            : new LocalDate(value.Year, value.Month, value.Day)
                .At(LocalTime.FromHourMinuteSecondNanosecond(
                    value.Hours, value.Minutes, value.Seconds, value.Nanos));
}
