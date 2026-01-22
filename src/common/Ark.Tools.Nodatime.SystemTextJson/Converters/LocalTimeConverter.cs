// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for local times, using the ISO-8601 time pattern.
/// </summary>
public sealed class LocalTimeConverter : JsonConverter<LocalTime>
{
    private readonly JsonConverter<LocalTime> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="LocalTimeConverter"/> class.
    /// </summary>
    public static LocalTimeConverter Instance { get; } = new LocalTimeConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTimeConverter"/> class.
    /// </summary>
    public LocalTimeConverter()
    {
        _inner = NodaConverters.LocalTimeConverter;
    }

    /// <inheritdoc />
    public override LocalTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, LocalTime value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
