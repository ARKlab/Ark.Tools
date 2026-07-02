// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="LocalDateTime"/> (a zoneless date and time).
/// The date components plus the nanosecond-of-day preserve the full NodaTime precision.
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

    /// <summary>Gets or sets the number of nanoseconds elapsed since midnight.</summary>
    [ProtoMember(4)]
    public long NanosecondOfDay { get; set; }

    /// <summary>Converts a <see cref="LocalDateTime"/> into its surrogate.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator LocalDateTimeSurrogate(LocalDateTime value)
        => new()
        {
            Year = value.Year,
            Month = value.Month,
            Day = value.Day,
            NanosecondOfDay = value.NanosecondOfDay,
        };

    /// <summary>Converts a surrogate back into a <see cref="LocalDateTime"/>.</summary>
    /// <param name="value">The surrogate to convert.</param>
    public static implicit operator LocalDateTime(LocalDateTimeSurrogate? value)
        => value is null
            ? default
            : new LocalDate(value.Year, value.Month, value.Day)
                .At(LocalTime.FromNanosecondsSinceMidnight(value.NanosecondOfDay));
}
