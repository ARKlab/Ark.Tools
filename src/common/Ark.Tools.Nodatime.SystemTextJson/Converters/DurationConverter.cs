// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using NodaTime.Text;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for durations using <see cref="DurationPattern.JsonRoundtrip"/>.
/// </summary>
public sealed class DurationConverter : JsonConverter<Duration>
{
    private readonly JsonConverter<Duration> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="DurationConverter"/> class.
    /// </summary>
    public static DurationConverter Instance { get; } = new DurationConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="DurationConverter"/> class.
    /// </summary>
    public DurationConverter()
    {
        _inner = (NodaConverters.DurationConverter as JsonConverter<Duration>)!;
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
