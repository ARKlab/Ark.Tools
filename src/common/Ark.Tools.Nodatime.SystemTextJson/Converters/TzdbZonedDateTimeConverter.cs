// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for zoned date/times using the Tzdb provider.
/// </summary>
public sealed class TzdbZonedDateTimeConverter : JsonConverter<ZonedDateTime>
{
    private readonly JsonConverter<ZonedDateTime> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="TzdbZonedDateTimeConverter"/> class.
    /// </summary>
    public static TzdbZonedDateTimeConverter Instance { get; } = new TzdbZonedDateTimeConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="TzdbZonedDateTimeConverter"/> class.
    /// </summary>
    public TzdbZonedDateTimeConverter()
    {
        _inner = NodaConverters.CreateZonedDateTimeConverter(DateTimeZoneProviders.Tzdb);
    }

    /// <inheritdoc />
    public override ZonedDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ZonedDateTime value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
