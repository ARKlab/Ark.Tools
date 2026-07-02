// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="LocalDate"/> (date only, no time and no zone).
/// The date components are stored explicitly so the ISO calendar value round-trips exactly.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[ProtoContract]
public sealed class LocalDateSurrogate
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

    /// <summary>Converts a <see cref="LocalDate"/> into its surrogate.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator LocalDateSurrogate(LocalDate value)
        => new() { Year = value.Year, Month = value.Month, Day = value.Day };

    /// <summary>Converts a surrogate back into a <see cref="LocalDate"/>.</summary>
    /// <param name="value">The surrogate to convert.</param>
    public static implicit operator LocalDate(LocalDateSurrogate? value)
        => value is null ? default : new LocalDate(value.Year, value.Month, value.Day);
}
