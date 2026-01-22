// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for time zones using the Tzdb provider.
/// </summary>
public sealed class TzdbDateTimeZoneConverter : JsonConverter<DateTimeZone>
{
    private readonly JsonConverter<DateTimeZone> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="TzdbDateTimeZoneConverter"/> class.
    /// </summary>
    public static TzdbDateTimeZoneConverter Instance { get; } = new TzdbDateTimeZoneConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="TzdbDateTimeZoneConverter"/> class.
    /// </summary>
    public TzdbDateTimeZoneConverter()
    {
        _inner = NodaConverters.CreateDateTimeZoneConverter(DateTimeZoneProviders.Tzdb);
    }

    /// <inheritdoc />
    public override DateTimeZone? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options) ?? throw new JsonException("Cannot deserialize DateTimeZone.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeZone value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
