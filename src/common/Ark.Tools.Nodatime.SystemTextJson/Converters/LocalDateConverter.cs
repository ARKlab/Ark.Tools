// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for local dates, using the ISO-8601 date pattern.
/// </summary>
public sealed class LocalDateConverter : JsonConverter<LocalDate>
{
    private readonly JsonConverter<LocalDate> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="LocalDateConverter"/> class.
    /// </summary>
    public static LocalDateConverter Instance { get; } = new LocalDateConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDateConverter"/> class.
    /// </summary>
    public LocalDateConverter()
    {
        _inner = NodaConverters.LocalDateConverter;
    }

    /// <inheritdoc />
    public override LocalDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LocalDate value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
