// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for local dates and times, using the ISO-8601 date/time pattern.
/// </summary>
public sealed class LocalDateTimeConverter : JsonConverter<LocalDateTime>
{
    private readonly JsonConverter<LocalDateTime> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="LocalDateTimeConverter"/> class.
    /// </summary>
    public static LocalDateTimeConverter Instance { get; } = new LocalDateTimeConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDateTimeConverter"/> class.
    /// </summary>
    public LocalDateTimeConverter()
    {
        _inner = NodaConverters.LocalDateTimeConverter;
    }

    /// <inheritdoc />
    public override LocalDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LocalDateTime value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
