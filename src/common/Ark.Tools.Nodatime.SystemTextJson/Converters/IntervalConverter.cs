// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for intervals.
/// </summary>
public sealed class IntervalConverter : JsonConverter<Interval>
{
    private readonly JsonConverter<Interval> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="IntervalConverter"/> class.
    /// </summary>
    public static IntervalConverter Instance { get; } = new IntervalConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalConverter"/> class.
    /// </summary>
    public IntervalConverter()
    {
        _inner = NodaConverters.IntervalConverter;
    }

    /// <inheritdoc />
    public override Interval Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Interval value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
