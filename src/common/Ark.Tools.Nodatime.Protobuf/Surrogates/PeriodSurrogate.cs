// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using NodaTime.Text;

using ProtoBuf;

namespace Ark.Tools.Nodatime.Protobuf;

/// <summary>
/// protobuf-net surrogate for <see cref="Period"/>, encoded as an ISO-8601 duration string
/// (for example <c>P1Y2M10DT2H30M</c>) using <see cref="PeriodPattern.Roundtrip"/>.
/// </summary>
[SuppressMessage("Design", "CA2225:Operator overloads have named alternates", Justification = "The implicit conversions are the protobuf-net surrogate contract; named alternates would be unused API surface.")]
[ProtoContract]
public sealed class PeriodSurrogate
{
    /// <summary>Gets or sets the ISO-8601 round-trippable representation of the period.</summary>
    [ProtoMember(1)]
    public string? Value { get; set; }

    /// <summary>Converts a <see cref="Period"/> into its surrogate.</summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator PeriodSurrogate?(NodaTime.Period? value)
        => value is null ? null : new PeriodSurrogate { Value = PeriodPattern.Roundtrip.Format(value) };

    /// <summary>Converts a surrogate back into a <see cref="Period"/>.</summary>
    /// <param name="value">The surrogate to convert.</param>
    public static implicit operator NodaTime.Period?(PeriodSurrogate? value)
        => string.IsNullOrEmpty(value?.Value) ? null : PeriodPattern.Roundtrip.Parse(value.Value).Value;
}
