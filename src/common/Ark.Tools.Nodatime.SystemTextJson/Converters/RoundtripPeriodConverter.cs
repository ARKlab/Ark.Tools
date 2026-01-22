// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Round-tripping converter for periods.
/// </summary>
public sealed class RoundtripPeriodConverter : JsonConverter<Period>
{
    private readonly JsonConverter<Period> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="RoundtripPeriodConverter"/> class.
    /// </summary>
    public static RoundtripPeriodConverter Instance { get; } = new RoundtripPeriodConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="RoundtripPeriodConverter"/> class.
    /// </summary>
    public RoundtripPeriodConverter()
    {
        _inner = NodaConverters.RoundtripPeriodConverter;
    }

    /// <inheritdoc />
    public override Period? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Period value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
