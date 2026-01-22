// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using NodaTime.Text;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for durations using <see cref="DurationPattern.Roundtrip"/>.
/// </summary>
public sealed class RoundtripDurationConverter : JsonConverter<Duration>
{
    private readonly JsonConverter<Duration> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="RoundtripDurationConverter"/> class.
    /// </summary>
    public static RoundtripDurationConverter Instance { get; } = new RoundtripDurationConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="RoundtripDurationConverter"/> class.
    /// </summary>
    public RoundtripDurationConverter()
    {
        _inner = (NodaConverters.RoundtripDurationConverter as JsonConverter<Duration>)!;
    }

    /// <inheritdoc />
    public override Duration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
