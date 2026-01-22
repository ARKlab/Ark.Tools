// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Ark.Tools.Nodatime.SystemTextJson.Converters;

/// <summary>
/// Converter for offset date/times.
/// </summary>
public sealed class OffsetDateTimeConverter : JsonConverter<OffsetDateTime>
{
    private readonly JsonConverter<OffsetDateTime> _inner;

    /// <summary>
    /// Gets the singleton instance of the <see cref="OffsetDateTimeConverter"/> class.
    /// </summary>
    public static OffsetDateTimeConverter Instance { get; } = new OffsetDateTimeConverter();

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetDateTimeConverter"/> class.
    /// </summary>
    public OffsetDateTimeConverter()
    {
        _inner = NodaConverters.OffsetDateTimeConverter;
    }

    /// <inheritdoc />
    public override OffsetDateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return _inner.Read(ref reader, typeToConvert, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OffsetDateTime value, JsonSerializerOptions options)
    {
        _inner.Write(writer, value, options);
    }
}
