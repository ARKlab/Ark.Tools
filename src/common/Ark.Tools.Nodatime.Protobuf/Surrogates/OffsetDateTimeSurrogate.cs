// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="OffsetDateTime"/>. The local date/time is stored
/// alongside the offset (in seconds) so the original UTC offset is preserved on the wire.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[ProtoContract]
public sealed class OffsetDateTimeSurrogate
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

    /// <summary>Gets or sets the number of nanoseconds elapsed since midnight of the local time.</summary>
    [ProtoMember(4)]
    public long NanosecondOfDay { get; set; }

    /// <summary>Gets or sets the UTC offset, expressed in whole seconds.</summary>
    [ProtoMember(5)]
    public int OffsetSeconds { get; set; }

    /// <summary>Converts an <see cref="OffsetDateTime"/> into its surrogate.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator OffsetDateTimeSurrogate(OffsetDateTime value)
        => new()
        {
            Year = value.Year,
            Month = value.Month,
            Day = value.Day,
            NanosecondOfDay = value.NanosecondOfDay,
            OffsetSeconds = value.Offset.Seconds,
        };

    /// <summary>Converts a surrogate back into an <see cref="OffsetDateTime"/>.</summary>
    /// <param name="value">The surrogate to convert.</param>
    public static implicit operator OffsetDateTime(OffsetDateTimeSurrogate? value)
    {
        if (value is null)
            return default;

        var local = new LocalDate(value.Year, value.Month, value.Day)
            .At(LocalTime.FromNanosecondsSinceMidnight(value.NanosecondOfDay));

        return new OffsetDateTime(local, Offset.FromSeconds(value.OffsetSeconds));
    }
}
